#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
using System;
using System.Collections.Generic;
using BossFight.Core;
using UnityEngine;

namespace BossFight.DebugTools
{
    /// <summary>
    /// T2 測試用木樁：輪流放出 TrainingDummyData 裡的攻擊（可擋閃白光、不可擋閃紅光），用來練格擋與盾反。
    /// 預兆期間地上畫出攻擊範圍，另有一圈從中心往外擴的計時圈，碰到外圈的那一刻就是命中。
    /// 本身也是 IDamageable，被打時頭上跳傷害數字（含失衡值），並常駐顯示上一下、這輪連擊與累計傷害；
    /// 玩家擋下、盾反或閃過時也會在上方顯示結果。由 Tools > Debug > Build Training Dummy 建立。
    /// </summary>
    [DisallowMultipleComponent]
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        enum Phase { Waiting, Telegraph, Active }

        struct Popup
        {
            public string text;
            public Color color;
            public float time;
            public float offsetX;
        }

        [SerializeField] TrainingDummyData data;
        [Tooltip("木樁的攻擊判定")]
        [SerializeField] Hitbox attackHitbox;
        [Tooltip("關掉就只當沙包、不出招（Play 中也可以切換）")]
        [SerializeField] bool attacksEnabled = true;

        [Header("外觀（純視覺）")]
        [Tooltip("預兆時發光的 Renderer（材質要開 Emission）")]
        [SerializeField] Renderer[] flashRenderers;
        [Tooltip("攻擊範圍")]
        [SerializeField] LineRenderer rangeRing;
        [Tooltip("往外擴的計時圈，碰到範圍圈時命中")]
        [SerializeField] LineRenderer timingRing;
        [Tooltip("受擊時搖晃的部分（以底座為軸）")]
        [SerializeField] Transform wobbleRoot;
        [Tooltip("傷害數字出現的位置（頭頂）")]
        [SerializeField] Transform labelAnchor;
        [Tooltip("把文字投到畫面上用的鏡頭（Main Camera）")]
        [SerializeField] Camera viewCamera;
        [Tooltip("白（可格擋）、紅（不可格擋）的分類色（所有攻擊共用一份）")]
        [SerializeField] TelegraphPaletteData palette;
        [Min(0f)] [SerializeField] float flashIntensity = 2.5f;
        [Range(8, 256)] [SerializeField] int ringSegments = 64;
        [SerializeField] float wobbleAngle = 6f;
        [Min(0.01f)] [SerializeField] float wobbleTime = 0.4f;
        [Tooltip("傷害數字往上飄的速度 (m/s)")]
        [SerializeField] float popupRiseSpeed = 0.6f;
        [Tooltip("傷害數字從標題上方多高 (m) 開始飄，才不會蓋住標題")]
        [SerializeField] float popupStartHeight = 0.5f;
        [Tooltip("連續的傷害數字左右錯開的距離（像素）")]
        [SerializeField] float popupSpread = 90f;

        /// <summary>被打；參數是收到的攻擊。</summary>
        public event Action<DamageInfo> OnDamaged;

        /// <summary>預兆中離命中還有幾秒；不在預兆時為 -1。</summary>
        public float TimeToImpact => phase == Phase.Telegraph ? Mathf.Max(0f, current.telegraphTime - timer) : -1f;
        /// <summary>目前（或上一招）的攻擊。</summary>
        public TrainingDummyData.DummyAttack CurrentAttack => current;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        Phase phase;
        float timer, waitTime;
        int attackIndex;
        TrainingDummyData.DummyAttack current;
        bool hitSomething;
        MaterialPropertyBlock block;
        Quaternion wobbleRest;
        float wobbleLeft;
        float lastDamage, comboDamage, totalDamage, lastHitTime = float.NegativeInfinity;
        int hitCount;
        readonly List<Popup> popups = new List<Popup>();
        GUIStyle popupStyle, headerStyle;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (wobbleRoot != null) wobbleRest = wobbleRoot.localRotation;
            attackHitbox.SetSphere(new Vector3(0f, data.attackCenterHeight, 0f), data.attackRadius);
            SetupRing(rangeRing);
            SetupRing(timingRing);
            SetCircle(rangeRing, data.attackRadius);
            ShowRings(false);
            SetFlash(Color.black);
            waitTime = data.startDelay;
        }

        void OnEnable()
        {
            attackHitbox.OnHit += HandleAttackHit;
        }

        void OnDisable()
        {
            attackHitbox.OnHit -= HandleAttackHit;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateWobble(dt);
            if (data.attacks == null || data.attacks.Length == 0) return;

            timer += dt;
            switch (phase)
            {
                case Phase.Waiting:
                    if (attacksEnabled && timer >= waitTime) BeginTelegraph();
                    break;
                case Phase.Telegraph:
                    float k = current.telegraphTime > 0f ? Mathf.Clamp01(timer / current.telegraphTime) : 1f;
                    SetFlash(AttackColor() * (flashIntensity * k * k));
                    SetCircle(timingRing, data.attackRadius * k);
                    if (timer >= current.telegraphTime) BeginActive();
                    break;
                case Phase.Active:
                    if (timer >= current.activeTime) EndAttack();
                    break;
            }
        }

        void BeginTelegraph()
        {
            current = data.attacks[attackIndex];
            phase = Phase.Telegraph;
            timer = 0f;
            Color c = AttackColor();
            SetRingColor(rangeRing, c);
            SetRingColor(timingRing, c);
            SetCircle(timingRing, 0f);
            ShowRings(true);
        }

        void BeginActive()
        {
            phase = Phase.Active;
            timer = 0f;
            hitSomething = false;
            SetFlash(AttackColor() * (flashIntensity * 2f));
            SetCircle(timingRing, data.attackRadius);
            attackHitbox.Activate(new DamageInfo(current.damage, current.canBlock, current.canParry, current.imbalance));
        }

        void EndAttack()
        {
            attackHitbox.Deactivate();
            if (!hitSomething) AddPopup("落空", new Color(0.7f, 0.7f, 0.7f));
            SetFlash(Color.black);
            ShowRings(false);
            attackIndex = (attackIndex + 1) % data.attacks.Length;
            phase = Phase.Waiting;
            timer = 0f;
            waitTime = data.attackInterval;
        }

        public DamageOutcome TakeDamage(in DamageInfo info)
        {
            if (Time.time - lastHitTime > data.comboResetTime) comboDamage = 0f;
            lastHitTime = Time.time;
            lastDamage = info.damage;
            comboDamage += info.damage;
            totalDamage += info.damage;
            hitCount++;

            string text = info.imbalance > 0f ? $"{info.damage:0}  失衡 +{info.imbalance:0}" : $"{info.damage:0}";
            AddPopup(text, new Color(1f, 0.85f, 0.3f));
            wobbleLeft = wobbleTime;
            OnDamaged?.Invoke(info);
            return DamageResult.Hit;
        }

        void HandleAttackHit(HitReport report)
        {
            hitSomething = true;
            switch (report.result)
            {
                case DamageResult.Hit:
                    AddPopup($"打中玩家 −{report.info.damage:0}", new Color(1f, 0.4f, 0.35f));
                    break;
                case DamageResult.Blocked:
                    AddPopup("被格擋", new Color(0.6f, 0.85f, 1f));
                    break;
                case DamageResult.GuardBroken:
                    AddPopup("破防！", new Color(1f, 0.55f, 0.2f));
                    break;
                case DamageResult.Parried:
                    AddPopup("被盾反！", new Color(1f, 0.9f, 0.2f));
                    break;
                case DamageResult.Evaded:
                    AddPopup("被閃過（無敵中）", new Color(0.7f, 1f, 0.7f));
                    break;
            }
        }

        Color AttackColor() => TelegraphPaletteData.Resolve(palette, this).Flash(current.canBlock);

        void AddPopup(string text, Color color)
        {
            // 左右錯開，連續的數字才不會疊在一起
            float offset = (popups.Count % 3 - 1) * popupSpread;
            popups.Add(new Popup { text = text, color = color, time = Time.time, offsetX = offset });
        }

        void UpdateWobble(float dt)
        {
            if (wobbleRoot == null) return;
            wobbleLeft = Mathf.Max(0f, wobbleLeft - dt);
            float k = wobbleLeft / wobbleTime;
            float angle = Mathf.Sin((1f - k) * Mathf.PI * 6f) * wobbleAngle * k;
            wobbleRoot.localRotation = wobbleRest * Quaternion.Euler(angle, 0f, 0f);
        }

        void SetFlash(Color emission)
        {
            if (flashRenderers == null) return;
            foreach (var r in flashRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, emission);
                r.SetPropertyBlock(block);
            }
        }

        void SetupRing(LineRenderer ring)
        {
            if (ring == null) return;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = ringSegments;
        }

        void SetCircle(LineRenderer ring, float radius)
        {
            if (ring == null) return;
            for (int i = 0; i < ringSegments; i++)
            {
                float a = i * Mathf.PI * 2f / ringSegments;
                ring.SetPosition(i, new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius));
            }
        }

        static void SetRingColor(LineRenderer ring, Color color)
        {
            if (ring == null) return;
            ring.startColor = color;
            ring.endColor = color;
        }

        void ShowRings(bool show)
        {
            if (rangeRing != null) rangeRing.enabled = show;
            if (timingRing != null) timingRing.enabled = show;
        }

        void OnGUI()
        {
            if (viewCamera == null || labelAnchor == null) return;
            if (popupStyle == null)
            {
                popupStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                popupStyle.normal.textColor = Color.white; // 顏色交給 GUI.color
                headerStyle = new GUIStyle(popupStyle) { fontSize = 16, fontStyle = FontStyle.Normal };
            }

            Vector3 anchor = labelAnchor.position;
            string header = $"木樁　上一下 {lastDamage:0}　連擊 {comboDamage:0}　累計 {totalDamage:0}（{hitCount} 下）";
            if (phase == Phase.Telegraph) header += $"\n{current.label}　{TimeToImpact:0.00} s";
            else if (!attacksEnabled) header += "\n（出招已關閉）";
            DrawLabel(anchor, 0f, header, Color.white, headerStyle, 1f);

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                float age = Time.time - popups[i].time;
                if (age > data.popupLifetime)
                {
                    popups.RemoveAt(i);
                    continue;
                }
                Vector3 pos = anchor + Vector3.up * (popupStartHeight + age * popupRiseSpeed);
                DrawLabel(pos, popups[i].offsetX, popups[i].text, popups[i].color, popupStyle, 1f - age / data.popupLifetime);
            }
        }

        void DrawLabel(Vector3 world, float screenOffsetX, string text, Color color, GUIStyle style, float alpha)
        {
            Vector3 sp = viewCamera.WorldToScreenPoint(world);
            if (sp.z <= 0f) return;
            var rect = new Rect(sp.x + screenOffsetX - 300f, Screen.height - sp.y - 30f, 600f, 60f);
            var shadow = rect;
            shadow.position += new Vector2(2f, 2f);
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.8f);
            GUI.Label(shadow, text, style);
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(rect, text, style);
            GUI.color = Color.white;
        }
    }
}
#endif
