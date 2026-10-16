using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BossFight.Hazards
{
    /// <summary>
    /// 地面範圍提示：用 URP Decal Projector 把攻擊範圍投在地上（材質用 Shaders/TelegraphDecal）。
    /// 外框標出範圍，裡面的填色由出招方用 SetFill 從 0 推到 1，填滿的那一刻就是判定發生的瞬間；
    /// 判定那一幀呼叫 Strike 讓整片範圍閃亮，Hide 淡出。可擋的招白色、不可擋的紅色（和 Boss 身上的預兆閃光同一套規則）。
    /// 圓形以中心定位（innerRadius &gt; 0 時是環）；長方形從起點往 direction 長出去，填色也從起點往前長。
    /// 每個實例用自己的材質複本，參數直接改材質。這裡的數字全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class GroundTelegraph : MonoBehaviour
    {
        [SerializeField] DecalProjector projector;

        [Header("顏色（HDR，A 是整體不透明度）")]
        [SerializeField] [ColorUsage(true, true)] Color blockableColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] [ColorUsage(true, true)] Color unblockableColor = new Color(1f, 0.12f, 0.08f, 0.95f);

        [Header("樣式")]
        [Tooltip("外框寬度 (m)")]
        [Min(0.01f)] [SerializeField] float edgeWidth = 0.12f;
        [Tooltip("已填滿部分的不透明度")]
        [Range(0f, 1f)] [SerializeField] float fillAlpha = 0.45f;
        [Tooltip("整個範圍的底色不透明度（還沒填到的地方）")]
        [Range(0f, 1f)] [SerializeField] float areaAlpha = 0.12f;
        [Tooltip("發光強度")]
        [Min(0f)] [SerializeField] float emission = 1.5f;
        [Tooltip("投影深度 (m)：一半在地面上、一半在地面下；太深會染到角色的腳和外牆底部")]
        [Min(0.05f)] [SerializeField] float depth = 0.3f;

        [Header("時間（秒）")]
        [Tooltip("Strike 的閃光衰減時間")]
        [Min(0.01f)] [SerializeField] float flashTime = 0.15f;
        [Tooltip("Hide 的淡出時間")]
        [Min(0.01f)] [SerializeField] float fadeTime = 0.3f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int ParamsId = Shader.PropertyToID("_Params");
        static readonly int StyleId = Shader.PropertyToID("_Style");
        static readonly int SizeId = Shader.PropertyToID("_Size");

        Material material;
        float shape, fill, inner, flash;
        float fadeTimer;
        bool visible, fading;

        public bool IsVisible => visible;

        void Awake()
        {
            material = new Material(projector.material);
            projector.material = material;
            HideImmediate();
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        /// <summary>以 groundCenter 為中心的圓（innerRadius &gt; 0 時是環），填色從 0 開始。</summary>
        public void ShowCircle(Vector3 groundCenter, float radius, bool blockable, float innerRadius = 0f)
        {
            shape = 0f;
            inner = radius > 0f ? Mathf.Clamp01(innerRadius / radius) : 0f;
            Place(groundCenter, Vector3.forward, new Vector2(radius * 2f, radius * 2f), 0f);
            Begin(blockable);
        }

        /// <summary>移動圓的位置與大小，不重設填色（跟著目標走的落點、擴散中的環）。</summary>
        public void MoveCircle(Vector3 groundCenter, float radius, float innerRadius = 0f)
        {
            inner = radius > 0f ? Mathf.Clamp01(innerRadius / radius) : 0f;
            Place(groundCenter, Vector3.forward, new Vector2(radius * 2f, radius * 2f), 0f);
        }

        /// <summary>從 groundStart 往 direction 長 length、寬 width 的長方形，填色從 0 開始。</summary>
        public void ShowRect(Vector3 groundStart, Vector3 direction, float length, float width, bool blockable)
        {
            shape = 1f;
            inner = 0f;
            Place(groundStart, direction, new Vector2(width, length), length * 0.5f);
            Begin(blockable);
        }

        /// <summary>移動長方形，不重設填色（預兆期間跟著 Boss 轉）。</summary>
        public void MoveRect(Vector3 groundStart, Vector3 direction, float length, float width)
        {
            Place(groundStart, direction, new Vector2(width, length), length * 0.5f);
        }

        /// <summary>填色進度 0..1；1 = 判定發生的瞬間。</summary>
        public void SetFill(float t) => fill = Mathf.Clamp01(t);

        /// <summary>判定發生：整片範圍閃亮一下。</summary>
        public void Strike() => flash = 1f;

        /// <summary>淡出後隱藏。</summary>
        public void Hide()
        {
            if (!visible) return;
            fading = true;
        }

        public void HideImmediate()
        {
            visible = fading = false;
            flash = 0f;
            projector.enabled = false;
        }

        void Begin(bool blockable)
        {
            fill = 0f;
            flash = 0f;
            fading = false;
            fadeTimer = 0f;
            visible = true;
            material.SetColor(ColorId, blockable ? blockableColor : unblockableColor);
            projector.fadeFactor = 1f;
            projector.enabled = true;
            Apply();
        }

        // 投影器朝下（+Z = 世界下方），+Y 是 direction，UV 的 V 沿 +Y 增加
        void Place(Vector3 ground, Vector3 direction, Vector2 size, float forwardOffset)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) direction = Vector3.forward;
            transform.SetPositionAndRotation(ground + Vector3.up * (depth * 0.5f),
                                             Quaternion.LookRotation(Vector3.down, direction.normalized));
            projector.size = new Vector3(size.x, size.y, depth);
            projector.pivot = new Vector3(0f, forwardOffset, depth * 0.5f);
            material.SetVector(SizeId, new Vector4(size.x, size.y, 0f, 0f));
        }

        void Update()
        {
            if (!visible) return;
            float dt = Time.deltaTime;
            if (flash > 0f) flash = Mathf.Max(0f, flash - dt / flashTime);
            if (fading)
            {
                fadeTimer += dt;
                projector.fadeFactor = 1f - Mathf.Clamp01(fadeTimer / fadeTime);
                if (fadeTimer >= fadeTime)
                {
                    HideImmediate();
                    return;
                }
            }
            Apply();
        }

        void Apply()
        {
            material.SetVector(ParamsId, new Vector4(shape, fill, inner, flash));
            material.SetVector(StyleId, new Vector4(edgeWidth, fillAlpha, areaAlpha, emission));
        }
    }
}
