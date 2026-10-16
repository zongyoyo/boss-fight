using BossFight.Core;
using UnityEngine;

namespace BossFight.Hazards
{
    /// <summary>
    /// 旋轉衝擊波：從中心往外擴散的環。判定是 Hitbox 的空心圓柱，每幀涵蓋「上一幀的內緣到這一幀的外緣」，速度快也不會跳過目標；
    /// 同一個目標整道波只結算一次。地上用 GroundTelegraph 畫出環帶，外緣立一圈發光的環牆，兩者都和判定同寬、同半徑。
    /// 擴散到最大半徑後收掉判定、淡出並刪掉自己。數值（速度、環寬、判定高度、最大半徑）由出招方從 ScriptableObject 傳入。
    /// </summary>
    [DisallowMultipleComponent]
    public class ShockwaveRing : Hazard
    {
        [Tooltip("地上的環帶（子物件）")]
        [SerializeField] GroundTelegraph band;
        [Tooltip("發光環牆：半徑 1、高 1 的開口圓柱，依目前半徑與判定高度縮放")]
        [SerializeField] Transform wall;
        [SerializeField] Renderer wallRenderer;
        [SerializeField] [ColorUsage(true, true)] Color wallColor = new Color(2.5f, 0.35f, 0.2f, 1f);
        [Tooltip("擴散到底後環牆淡出的時間（秒）")]
        [Min(0.01f)] [SerializeField] float fadeTime = 0.25f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock block;
        float radius, maxRadius, speed, bandWidth, height;
        float fadeTimer;
        bool running, fadingOut;

        public float Radius => radius;

        void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        /// <summary>開始擴散：環從 startRadius 以 expandSpeed (m/s) 長到 endRadius，環寬 width、判定高 hitHeight。</summary>
        public void Launch(in DamageInfo damage, float startRadius, float endRadius, float expandSpeed, float width, float hitHeight)
        {
            radius = startRadius;
            maxRadius = Mathf.Max(startRadius, endRadius);
            speed = expandSpeed;
            bandWidth = width;
            height = hitHeight;
            running = true;
            fadingOut = false;

            hitbox.SetCylinder(new Vector3(0f, height * 0.5f, 0f), radius, height, Mathf.Max(0f, radius - bandWidth));
            hitbox.Activate(damage);
            band.ShowCircle(transform.position, radius, false, Mathf.Max(0f, radius - bandWidth));
            band.SetFill(1f);
            UpdateWall(1f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (running)
            {
                float prev = radius;
                radius = Mathf.Min(maxRadius, radius + speed * dt);
                hitbox.SetCylinder(new Vector3(0f, height * 0.5f, 0f), radius, height, Mathf.Max(0f, prev - bandWidth));
                band.MoveCircle(transform.position, radius, Mathf.Max(0f, radius - bandWidth));
                UpdateWall(1f);
                if (radius >= maxRadius)
                {
                    running = false;
                    fadingOut = true;
                    fadeTimer = 0f;
                    hitbox.Deactivate();
                    band.Hide();
                }
            }
            else if (fadingOut)
            {
                fadeTimer += dt;
                float k = 1f - Mathf.Clamp01(fadeTimer / fadeTime);
                UpdateWall(k);
                if (k <= 0f && !band.IsVisible) Finish();
            }
        }

        void UpdateWall(float intensity)
        {
            if (wall != null) wall.localScale = new Vector3(radius, height, radius);
            if (wallRenderer == null) return;
            wallRenderer.GetPropertyBlock(block);
            Color c = wallColor * intensity;
            c.a = wallColor.a * intensity;
            block.SetColor(BaseColorId, c);
            wallRenderer.SetPropertyBlock(block);
        }
    }
}
