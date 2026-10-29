using BossFight.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BossFight.Hazards
{
    /// <summary>
    /// 地面範圍提示：用 URP Decal Projector 把攻擊範圍投在地上（材質用 Shaders/TelegraphDecal）。
    /// 外框標出範圍，裡面的填色由出招方用 SetFill 從 0 推到 1，填滿的那一刻就是判定發生的瞬間；
    /// 判定那一幀呼叫 Strike 讓整片範圍閃亮，Hide 淡出。可擋的招白色、不可擋的紅色（和 Boss 身上的預兆閃光同一套規則）。
    /// 圓形以中心定位（innerRadius &gt; 0 時是環）；長方形從起點往 direction 長出去，填色也從起點往前長。
    /// 序章修正（C1、C7）加上扇形（由中心往外填）、十字（由中心沿四條臂往外填）、全場（由填色起點往外填），
    /// 以及 SetClip 裁切到場地內、SetHoles 挖掉最多 3 個安全圈；安全區的綠色（SafeColor）也在這裡，跟紅、白同一個 prefab。
    /// 圓與環可以 SetSegments 切成等角度的小段（樹根環障一根刺一段，試玩回饋）。
    /// 每個實例用自己的材質複本，參數直接改材質。這裡的數字全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class GroundTelegraph : MonoBehaviour
    {
        [SerializeField] DecalProjector projector;

        [Tooltip("紅、白、安全區綠的分類色（所有攻擊共用一份）")]
        [SerializeField] TelegraphPaletteData palette;

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
        static readonly int FieldId = Shader.PropertyToID("_Field");
        static readonly int[] HoleIds = { Shader.PropertyToID("_Hole0"), Shader.PropertyToID("_Hole1"), Shader.PropertyToID("_Hole2") };
        const int MaxHoles = 3;

        Material material;
        float shape, fill, inner, flash;
        float fadeTimer;
        bool visible, fading;
        // 投影範圍 (m) 與形狀的額外參數（扇形角度、十字臂寬）；全場的填色起點、裁切與安全圈都存世界座標，Apply 時換成投影座標
        Vector2 boxSize;
        float extra;
        bool hasSource, hasClip;
        Vector3 source, clipCenter;
        float clipRadius;
        readonly Vector3[] holes = new Vector3[MaxHoles];
        int holeCount;
        float holeRadius;
        int segmentCount;
        float segmentPhase, segmentHalfWidth;

        public bool IsVisible => visible;
        TelegraphPaletteData Palette => TelegraphPaletteData.Resolve(palette, this);
        public Color BlockableColor => Palette.groundBlockable;
        public Color UnblockableColor => Palette.groundUnblockable;
        public Color SafeColor => Palette.groundSafe;
        public Color ColorFor(bool blockable) => Palette.Ground(blockable);

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
        public void ShowCircle(Vector3 groundCenter, float radius, bool blockable, float innerRadius = 0f) =>
            ShowCircle(groundCenter, radius, ColorFor(blockable), innerRadius);

        /// <summary>同上，但顏色另外指定（HDR，A 是整體不透明度），例如大迴旋的安全區。</summary>
        public void ShowCircle(Vector3 groundCenter, float radius, Color tint, float innerRadius = 0f)
        {
            shape = 0f;
            inner = radius > 0f ? Mathf.Clamp01(innerRadius / radius) : 0f;
            Place(groundCenter, Vector3.forward, new Vector2(radius * 2f, radius * 2f), 0f);
            Begin(tint);
        }

        /// <summary>移動圓的位置與大小，不重設填色（跟著目標走的落點、擴散中的環）。</summary>
        public void MoveCircle(Vector3 groundCenter, float radius, float innerRadius = 0f)
        {
            inner = radius > 0f ? Mathf.Clamp01(innerRadius / radius) : 0f;
            Place(groundCenter, Vector3.forward, new Vector2(radius * 2f, radius * 2f), 0f);
        }

        /// <summary>從 groundStart 往 direction 長 length、寬 width 的長方形，填色從 0 開始。</summary>
        public void ShowRect(Vector3 groundStart, Vector3 direction, float length, float width, bool blockable) =>
            ShowRect(groundStart, direction, length, width, ColorFor(blockable));

        /// <summary>同上，但顏色另外指定。</summary>
        public void ShowRect(Vector3 groundStart, Vector3 direction, float length, float width, Color tint)
        {
            shape = 1f;
            inner = 0f;
            Place(groundStart, direction, new Vector2(width, length), length * 0.5f);
            Begin(tint);
        }

        /// <summary>以 groundCenter 為圓心、往 direction 張開 angle 度、半徑 radius 的扇形，填色由圓心往外。</summary>
        public void ShowSector(Vector3 groundCenter, Vector3 direction, float radius, float angle, Color tint)
        {
            shape = 2f;
            inner = 0f;
            extra = angle;
            Place(groundCenter, direction, new Vector2(radius * 2f, radius * 2f), 0f);
            Begin(tint);
        }

        /// <summary>以 groundCenter 為中心、沿世界南北與東西向的十字，每條臂寬 armWidth、從中心延伸 reach，填色由中心沿四條臂往外。</summary>
        public void ShowCross(Vector3 groundCenter, float armWidth, float reach, Color tint)
        {
            shape = 3f;
            inner = 0f;
            extra = armWidth;
            Place(groundCenter, Vector3.forward, new Vector2(reach * 2f, reach * 2f), 0f);
            Begin(tint);
        }

        /// <summary>整個場地（以 fieldCenter 為中心、半徑 fieldRadius），填色由 fillOrigin（攻擊源）往外；搭配 SetHoles 挖安全圈。</summary>
        public void ShowField(Vector3 fieldCenter, float fieldRadius, Vector3 fillOrigin, Color tint)
        {
            shape = 4f;
            inner = 0f;
            Place(fieldCenter, Vector3.forward, new Vector2(fieldRadius * 2f, fieldRadius * 2f), 0f);
            Begin(tint);
            hasSource = true;
            source = fillOrigin;
            Apply();
        }

        /// <summary>裁切到以 center 為中心、半徑 radius 的場地內（邊緣也畫外框）；radius ≤ 0 取消。Show 會重設。</summary>
        public void SetClip(Vector3 center, float radius)
        {
            hasClip = radius > 0f;
            clipCenter = center;
            clipRadius = radius;
            if (visible) Apply();
        }

        /// <summary>挖掉最多 3 個半徑 radius 的安全圈（圈邊畫外框）；null 取消。Show 會重設。</summary>
        public void SetHoles(Vector3[] centers, float radius)
        {
            holeCount = centers != null ? Mathf.Min(centers.Length, MaxHoles) : 0;
            for (int i = 0; i < holeCount; i++) holes[i] = centers[i];
            holeRadius = radius;
            if (visible) Apply();
        }

        /// <summary>
        /// 圓或環只畫 count 個等角度的小段（每段沿弧長半寬 halfWidth m，第一段在世界角度 phase 度：0 = 北、順時針）；count 0 取消。Show 會重設。
        /// </summary>
        public void SetSegments(int count, float phase, float halfWidth)
        {
            segmentCount = Mathf.Max(0, count);
            segmentPhase = phase;
            segmentHalfWidth = halfWidth;
            if (visible) Apply();
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

        void Begin(Color tint)
        {
            hasSource = hasClip = false;
            holeCount = 0;
            segmentCount = 0;
            fill = 0f;
            flash = 0f;
            fading = false;
            fadeTimer = 0f;
            visible = true;
            material.SetColor(ColorId, tint);
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
            boxSize = size;
        }

        // 世界座標 → 投影座標（相對投影範圍中心，x 向右、y 沿 direction，m）
        Vector2 Local(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            return new Vector2(local.x - projector.pivot.x, local.y - projector.pivot.y);
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
            Vector2 origin = hasSource ? Local(source) : Vector2.zero;
            // 圓／環的分段放在其他形狀才用的 Size.zw 與 Field.w（投影的 +y 是世界北方，見 ShowCircle）
            bool segmented = shape == 0f && segmentCount > 0;
            material.SetVector(SizeId, segmented ? new Vector4(boxSize.x, boxSize.y, segmentCount, segmentHalfWidth)
                : new Vector4(boxSize.x, boxSize.y, hasSource ? origin.x : extra, hasSource ? origin.y : 0f));
            Vector2 clip = hasClip ? Local(clipCenter) : Vector2.zero;
            material.SetVector(FieldId, new Vector4(clip.x, clip.y, hasClip ? clipRadius : 0f, segmented ? segmentPhase : 0f));
            for (int i = 0; i < MaxHoles; i++)
            {
                Vector2 hole = i < holeCount ? Local(holes[i]) : Vector2.zero;
                material.SetVector(HoleIds[i], new Vector4(hole.x, hole.y, i < holeCount ? holeRadius : 0f, 0f));
            }
        }
    }
}
