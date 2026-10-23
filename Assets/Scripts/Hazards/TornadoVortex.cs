using BossFight.Core;
using UnityEngine;

namespace BossFight.Hazards
{
    /// <summary>
    /// OIIA 龍捲風的漩渦：跟著 follow（Boss）在地上移動，地上一個紅圈（GroundTelegraph，不可擋）標出判定範圍。
    /// Form：從地面長出來（預兆，還沒有判定），紅圈在這段時間內填滿；Begin：開始判定，每 tickInterval 秒重新啟動一次 Hitbox
    /// （同一個目標每次只結算一次），也就是每 0.5 秒一跳；Dissipate：收掉判定、縮小淡出後刪掉自己。
    /// 打到目標的那一刻重新起算下一跳。目標在無敵中（受擊後的無敵剛好也是 0.5 秒、翻滾）而閃過時，下一幀再試，
    /// 出了無敵還站在漩渦裡就照樣挨這一跳；同一跳只回報一次閃過，不會每幀冒火花。
    /// 外觀是幾層由下往上越來越大、各自旋轉的環牆（半徑 1、高 1 的開口圓柱，和衝擊波同一個網格）。
    /// 數值（半徑、判定高度、間隔）由出招方從 ScriptableObject 傳入，這裡只放純視覺的參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class TornadoVortex : Hazard
    {
        [Tooltip("地上的紅圈（子物件）")]
        [SerializeField] GroundTelegraph circle;
        [Tooltip("漩渦的每一層（由下往上），半徑 1、高 1 的開口圓柱")]
        [SerializeField] Transform[] layers;
        [SerializeField] Renderer[] layerRenderers;
        [SerializeField] [ColorUsage(true, true)] Color color = new Color(1.4f, 1.2f, 1.6f, 0.8f);
        [Tooltip("最下層、最上層的半徑倍率（乘上判定半徑）")]
        [Min(0f)] [SerializeField] float bottomScale = 0.45f;
        [Min(0f)] [SerializeField] float topScale = 1.5f;
        [Tooltip("每一層的旋轉速度（度/秒），相鄰兩層反向")]
        [SerializeField] float spinSpeed = 720f;
        [Tooltip("Dissipate 後縮小淡出的時間（秒）")]
        [Min(0.01f)] [SerializeField] float fadeTime = 0.4f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock block;
        Transform follow;
        DamageInfo damage;
        float radius, height, interval, formTime;
        float timer, tickTimer, angle;
        bool forming, running, fading, retry, evadedReported;

        void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        /// <summary>預兆：跟著 target 從地面長出來，duration 秒後長滿；判定半徑 hitRadius、高 hitHeight 用來決定大小。</summary>
        public void Form(Transform target, float hitRadius, float hitHeight, float duration)
        {
            follow = target;
            radius = hitRadius;
            height = hitHeight;
            formTime = duration;
            timer = 0f;
            forming = true;
            running = fading = false;
            circle.ShowCircle(GroundPosition(), radius, false);
            circle.SetFill(0f);
            UpdateVisual(0f);
        }

        /// <summary>開始判定：立刻打一跳，之後每 tickInterval 秒一跳。</summary>
        public void Begin(in DamageInfo info, float tickInterval)
        {
            damage = info;
            interval = Mathf.Max(0.01f, tickInterval);
            forming = false;
            running = true;
            tickTimer = 0f;
            retry = evadedReported = false;
            hitbox.SetCylinder(new Vector3(0f, height * 0.5f, 0f), radius, height);
            hitbox.Activate(damage);
            circle.SetFill(1f);
            circle.Strike();
        }

        /// <summary>結束：收掉判定，縮小淡出後刪掉自己。</summary>
        public void Dissipate()
        {
            if (fading) return;
            forming = running = false;
            fading = true;
            timer = 0f;
            hitbox.Deactivate();
            circle.Hide();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            transform.position = GroundPosition();
            circle.MoveCircle(transform.position, radius);
            angle = Mathf.Repeat(angle + spinSpeed * dt, 360f);

            if (forming)
            {
                timer += dt;
                float t = formTime > 0f ? Mathf.Clamp01(timer / formTime) : 1f;
                circle.SetFill(t);
                UpdateVisual(t);
            }
            else if (running)
            {
                tickTimer += dt;
                if (tickTimer >= interval)
                {
                    tickTimer -= interval;
                    retry = evadedReported = false;
                    hitbox.Activate(damage); // 重新啟動 = 已經打過的目標可以再被打一次
                }
                else if (retry)
                {
                    retry = false;
                    hitbox.Activate(damage);
                }
                UpdateVisual(1f);
            }
            else if (fading)
            {
                timer += dt;
                float k = 1f - Mathf.Clamp01(timer / fadeTime);
                UpdateVisual(k);
                if (k <= 0f && !circle.IsVisible) Finish();
            }
        }

        protected override void HandleHit(HitReport report)
        {
            if (report.result == DamageResult.Evaded)
            {
                retry = running;
                if (evadedReported) return;
                evadedReported = true;
            }
            else if (report.result != DamageResult.Ignored)
            {
                tickTimer = 0f; // 從打到的這一刻起算下一跳
            }
            base.HandleHit(report);
        }

        Vector3 GroundPosition()
        {
            if (follow == null) return transform.position;
            Vector3 p = follow.position;
            return new Vector3(p.x, transform.position.y, p.z);
        }

        // amount 0..1：漩渦長出來的程度（高度、寬度、亮度一起變）
        void UpdateVisual(float amount)
        {
            int n = layers != null ? layers.Length : 0;
            for (int i = 0; i < n; i++)
            {
                if (layers[i] == null) continue;
                float u = n > 1 ? (float)i / (n - 1) : 0f;
                float r = radius * Mathf.Lerp(bottomScale, topScale, u) * Mathf.Lerp(0.3f, 1f, amount);
                float layerHeight = height / n;
                layers[i].localPosition = Vector3.up * (layerHeight * i * amount);
                layers[i].localRotation = Quaternion.Euler(0f, (i % 2 == 0 ? angle : -angle) + i * 37f, 0f);
                layers[i].localScale = new Vector3(r, layerHeight * amount * 1.6f, r);
            }
            if (layerRenderers == null) return;
            Color c = color * amount;
            c.a = color.a * amount;
            foreach (var renderer in layerRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
