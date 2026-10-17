using BossFight.Core;
using UnityEngine;

namespace BossFight.Player
{
    /// <summary>
    /// 刀盾狗的程序化動畫（不用 Animator Controller）：走路擺腿、身體搖擺、舉盾、翻滾、空中收腿、搖尾巴（有尾巴時），
    /// 以及三段連擊、蓄力與重擊、處決、啃骨頭、破防、受擊、盾反、倒地，受擊後的無敵時間會閃爍。
    /// 只改各關節 pivot 的 localRotation / localPosition（骨頭另外改大小），pivot 由 Editor/DaoDunDogBuilder 建立並指派。
    /// 狀態讀 PlayerController 與 PlayerAttack；受擊與盾反訂閱 PlayerHealth 的事件。這裡的數字全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class DaoDunDogAnimator : MonoBehaviour
    {
        [System.Serializable]
        public struct SwingKey
        {
            [Tooltip("右臂相對靜止姿勢的 Euler")]
            public Vector3 arm;
            [Tooltip("手腕：刀相對握姿的 Euler，+X 讓刀身往刀刃那側倒")]
            public Vector3 wrist;
            [Tooltip("身體扭轉（度），正值往右轉、右肩往後")]
            public float twist;

            public SwingKey(Vector3 arm, Vector3 wrist, float twist)
            {
                this.arm = arm;
                this.wrist = wrist;
                this.twist = twist;
            }
        }

        public PlayerController controller;
        public PlayerAttack attack;
        public PlayerHealth health;

        [Header("關節（由 DaoDunDogBuilder 指派）")]
        [Tooltip("整個模型的根；翻滾時整個轉")]
        public Transform model;
        public Transform body;
        public Transform head;
        public Transform armR;
        [Tooltip("短刀的握點（右手腕）")]
        public Transform sword;
        public Transform armL;
        [Tooltip("盾牌握把，掛在左臂下；朝向在身體空間控制，揮臂時盾面不會亂轉")]
        public Transform shieldGrip;
        public Transform legR;
        public Transform legL;
        [Tooltip("可留空")]
        public Transform tail;
        [Tooltip("啃骨頭時叼在嘴裡的骨頭，平常隱藏")]
        public Transform bone;

        [Header("步伐")]
        [Tooltip("一個完整步伐循環前進的距離 (m)")]
        [Min(0.1f)] public float strideLength = 0.9f;
        public float legSwing = 35f;
        public float armSwing = 10f;
        public float bobHeight = 0.04f;
        [Tooltip("走路時左右搖擺（青蛙腳的鴨子步）")]
        public float waddle = 10f;
        public float runLean = 10f;

        [Header("揮刀時間軸")]
        [Tooltip("判定開始前多久（秒）從 key0 開始揮下；判定開始那一刻剛好揮到 key1（砍到），傷害數字才不會比刀先到")]
        [Min(0f)] public float swingLeadTime = 0.08f;

        [Header("揮刀關鍵姿勢：舉刀 → 砍到（判定開始）→ 揮完（各 3 個）")]
        [Tooltip("第一刀：右上往左下斜劈")]
        public SwingKey[] forehand =
        {
            new SwingKey(new Vector3(-130f, 0f, 25f), Vector3.zero, 25f),
            new SwingKey(new Vector3(-75f, -10f, 10f), Vector3.zero, 0f),
            new SwingKey(new Vector3(45f, -45f, -10f), new Vector3(10f, 0f, 0f), -25f),
        };
        [Tooltip("第二刀：收手後往前刺（手太短，橫掃會埋進肚子）")]
        public SwingKey[] thrust =
        {
            new SwingKey(new Vector3(30f, 10f, 5f), new Vector3(5f, 0f, 0f), 25f),
            new SwingKey(new Vector3(-40f, -5f, 0f), new Vector3(85f, 0f, 0f), -10f),
            new SwingKey(new Vector3(-45f, -8f, 0f), new Vector3(85f, 0f, 0f), -20f),
        };
        [Tooltip("第三刀：舉過頭頂往前下劈，配合踏步")]
        public SwingKey[] finisher =
        {
            new SwingKey(new Vector3(-165f, 0f, 5f), new Vector3(-30f, 0f, 0f), 10f),
            new SwingKey(new Vector3(-90f, 0f, 0f), new Vector3(10f, 0f, 0f), 0f),
            new SwingKey(new Vector3(20f, -10f, 0f), new Vector3(40f, 0f, 0f), -10f),
        };
        [Tooltip("重擊：key0 也是蓄力時舉刀的姿勢；手腕約 +95 讓刀身順著手臂（舉高時朝上、肩後視角看得到），" +
                 "key1 刀往前砍到木樁的高度，key2 一路劈到地面")]
        public SwingKey[] heavy =
        {
            new SwingKey(new Vector3(-145f, 0f, 35f), new Vector3(95f, 0f, 0f), 25f),
            new SwingKey(new Vector3(-55f, -10f, 10f), new Vector3(100f, 0f, 0f), 0f),
            new SwingKey(new Vector3(10f, -20f, -10f), new Vector3(110f, 0f, 0f), -25f),
        };
        [Tooltip("處決：踏步時把刀高高舉過頭（刀尖朝後），砍到時往前下劈進貓的身體，最後一路劈到底")]
        public SwingKey[] execution =
        {
            new SwingKey(new Vector3(-175f, 0f, 10f), new Vector3(70f, 0f, 0f), 15f),
            new SwingKey(new Vector3(-80f, 0f, 0f), new Vector3(60f, 0f, 0f), 0f),
            new SwingKey(new Vector3(15f, -10f, 0f), new Vector3(80f, 0f, 0f), -15f),
        };
        [Tooltip("處決時身體往前傾（度），配合往下劈")]
        public float executionLean = 12f;

        [Header("蓄力")]
        [Tooltip("蓄力進度到這裡才開始舉刀，點擊（輕攻擊）不會閃一下蓄力姿勢；之後等速舉高，蓄滿的那一刻剛好舉到最高、馬上劈下")]
        [Range(0f, 0.9f)] public float chargePoseDelay = 0.2f;
        [Tooltip("快蓄滿時刀的抖動（度）")]
        public float chargeTremble = 3f;
        [Tooltip("蓄力時身體往後仰（度）")]
        public float chargeLean = -8f;

        [Header("舉盾")]
        [Tooltip("左臂舉盾姿勢（相對靜止姿勢的 Euler）")]
        public Vector3 blockArmPose = new Vector3(-75f, 40f, 0f);
        [Tooltip("舉盾時盾面在身體空間的朝向（Euler，0 = 正前方）")]
        public Vector3 blockShieldFacing = new Vector3(0f, -5f, 0f);
        [Min(0f)] public float blockBlendSpeed = 12f;

        [Header("盾反")]
        [Tooltip("盾反成功時左臂往前推盾的姿勢")]
        public Vector3 parryArmPose = new Vector3(-90f, 15f, 0f);
        [Min(0.01f)] public float parryPoseTime = 0.25f;

        [Header("破防")]
        [Tooltip("盾被撞開時的左臂姿勢")]
        public Vector3 guardBreakArmPose = new Vector3(-140f, -20f, -50f);
        [Tooltip("破防時身體往後仰（度）")]
        public float guardBreakLean = -18f;
        [Min(0f)] public float guardBreakBlendSpeed = 10f;

        [Header("受擊")]
        [Tooltip("被打中時身體往後仰（度），之後慢慢回正")]
        public float hurtLean = -18f;
        [Min(0.01f)] public float hurtRecoverTime = 0.3f;
        [Tooltip("受擊無敵時的閃爍頻率（次/秒）")]
        [Min(0f)] public float invulnerableBlinkRate = 12f;

        [Header("啃骨頭")]
        [Tooltip("咀嚼頻率（次/秒）")]
        public float chewRate = 6f;
        [Tooltip("咀嚼時頭上下擺動（度）")]
        public float chewAngle = 8f;
        [Tooltip("啃骨頭時抬頭（度，負值往上）；抬高一點，骨頭才會露出頭頂，肩後視角看得到")]
        public float chewHeadPitch = -25f;
        [Tooltip("咀嚼時頭左右歪（度），從背後也看得出在啃")]
        public float chewRoll = 14f;
        [Tooltip("咀嚼時身體上下抖動的高度 (m)")]
        public float chewBob = 0.05f;
        [Tooltip("啃骨頭時右臂姿勢（相對靜止姿勢的 Euler）：舉到骨頭那一端")]
        public Vector3 healArmRPose = new Vector3(-145f, 0f, 50f);
        [Tooltip("啃骨頭時左臂姿勢（相對靜止姿勢的 Euler）：舉到骨頭另一端")]
        public Vector3 healArmLPose = new Vector3(-145f, 0f, -50f);
        [Tooltip("骨頭跟著啃咬左右擺動（度）")]
        public float boneWiggle = 12f;
        [Tooltip("啃完時骨頭剩下的大小比例，看得出越啃越少")]
        [Range(0f, 1f)] public float boneEatenScale = 0.55f;

        [Header("倒地")]
        [Min(0.01f)] public float deathFallTime = 0.5f;
        [Tooltip("側躺後往下沉，讓身體貼地 (m)")]
        public float deathSink = 0.1f;

        [Header("翻滾")]
        [Tooltip("翻滾時模型繞這個點（角色局部座標）轉一圈，約在身體中心")]
        public Vector3 rollPivot = new Vector3(0f, 0.6f, 0f);

        Quaternion modelRest, bodyRest, headRest, armRRest, armLRest, legRRest, legLRest, tailRest, swordRest, boneRest;
        Quaternion shieldRestInBody;
        Vector3 modelRestPos, bodyRestPos, boneRestScale;
        float phase, moveWeight, airWeight, blockWeight, guardBreakWeight, healWeight, deathWeight;
        float hurtTimer, parryTimer;
        Renderer[] renderers;
        bool blinkHidden;
        // 上一幀的揮刀姿勢；出手時從這裡開始舉刀，蓄力中放開或連擊接段才不會跳回靜止姿勢
        Quaternion lastArm = Quaternion.identity, lastWrist = Quaternion.identity, fromArm = Quaternion.identity, fromWrist = Quaternion.identity;
        float lastTwist, fromTwist;

        void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            if (attack == null) attack = GetComponentInParent<PlayerAttack>();
            if (health == null) health = GetComponentInParent<PlayerHealth>();
            if (model != null)
            {
                modelRest = model.localRotation;
                modelRestPos = model.localPosition;
            }
            bodyRest = body.localRotation;
            bodyRestPos = body.localPosition;
            headRest = head.localRotation;
            armRRest = armR.localRotation;
            armLRest = armL.localRotation;
            legRRest = legR.localRotation;
            legLRest = legL.localRotation;
            if (tail != null) tailRest = tail.localRotation;
            if (sword != null) swordRest = sword.localRotation;
            shieldRestInBody = armLRest * shieldGrip.localRotation;
            renderers = GetComponentsInChildren<Renderer>(true);
            if (bone != null)
            {
                boneRest = bone.localRotation;
                boneRestScale = bone.localScale;
                bone.gameObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            if (attack != null) attack.OnAttackStarted += HandleAttackStarted;
            if (health == null) return;
            health.OnDamageTaken += HandleDamageTaken;
            health.OnParry += HandleParry;
        }

        void OnDisable()
        {
            if (attack != null) attack.OnAttackStarted -= HandleAttackStarted;
            if (health == null) return;
            health.OnDamageTaken -= HandleDamageTaken;
            health.OnParry -= HandleParry;
        }

        void HandleAttackStarted(int comboIndex, bool heavy)
        {
            fromArm = lastArm;
            fromWrist = lastWrist;
            fromTwist = lastTwist;
        }

        void HandleDamageTaken(PlayerDamageReport report)
        {
            if (report.result == DamageResult.Hit) hurtTimer = hurtRecoverTime;
        }

        void HandleParry(DamageInfo info, float imbalance)
        {
            parryTimer = parryPoseTime;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            var c = controller;
            var action = c != null ? c.Action : PlayerAction.None;
            float speed = c != null ? c.PlanarSpeed : 0f;
            float run = c != null ? Mathf.Max(0.01f, c.MoveSpeed) : 1f;
            bool grounded = c == null || c.IsGrounded;
            bool rolling = c != null && c.IsRolling;

            // 空中或翻滾時收腿；翻滾很短，收得快一點
            moveWeight = Mathf.MoveTowards(moveWeight, grounded && !rolling ? Mathf.Clamp01(speed / run) : 0f, dt * 6f);
            airWeight = Mathf.MoveTowards(airWeight, grounded && !rolling ? 0f : 1f, dt * (rolling ? 16f : 8f));
            blockWeight = Mathf.MoveTowards(blockWeight, c != null && c.IsBlocking ? 1f : 0f, dt * blockBlendSpeed);
            guardBreakWeight = Mathf.MoveTowards(guardBreakWeight, action == PlayerAction.GuardBreak ? 1f : 0f, dt * guardBreakBlendSpeed);
            healWeight = Mathf.MoveTowards(healWeight, action == PlayerAction.Heal ? 1f : 0f, dt * 8f);
            deathWeight = Mathf.MoveTowards(deathWeight, action == PlayerAction.Dead ? 1f : 0f, dt / deathFallTime);
            hurtTimer = Mathf.Max(0f, hurtTimer - dt);
            parryTimer = Mathf.Max(0f, parryTimer - dt);

            phase = Mathf.Repeat(phase + speed / strideLength * dt, 1f);
            float s = Mathf.Sin(phase * 2f * Mathf.PI);
            float bob = Mathf.Abs(Mathf.Cos(phase * 2f * Mathf.PI)) * bobHeight * moveWeight;
            float breathe = Mathf.Sin(Time.time * 2.2f) * 0.012f * (1f - moveWeight);

            // 翻滾：整隻狗繞身體中心往翻滾方向轉一圈（方向可以是側面或後面，跟面向無關）；倒地：往右側躺下
            if (model != null)
            {
                Quaternion roll = Quaternion.identity;
                if (rolling)
                {
                    Vector3 axis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, c.RollDirection));
                    roll = Quaternion.AngleAxis(360f * Mathf.SmoothStep(0f, 1f, c.RollProgress), axis);
                }
                float fall = Mathf.SmoothStep(0f, 1f, deathWeight);
                roll = Quaternion.AngleAxis(-90f * fall, Vector3.forward) * roll;
                model.localRotation = roll * modelRest;
                model.localPosition = rollPivot + roll * (modelRestPos - rollPivot) + Vector3.down * (deathSink * fall);
            }

            // 揮刀或蓄力的右臂、手腕、扭腰
            ArmPose(c, out Quaternion swingArm, out Quaternion swingWrist, out float twist, out float lean);
            float hurt = hurtTimer / hurtRecoverTime;
            lean += hurtLean * hurt * hurt + guardBreakLean * guardBreakWeight;

            // 啃骨頭：頭上下咀嚼、左右歪，身體跟著一抖一抖（從肩後視角也看得出來）
            float chewPhase = Time.time * chewRate * Mathf.PI;
            float chewPitch = (chewHeadPitch + Mathf.Sin(chewPhase * 2f) * chewAngle) * healWeight;
            float chewTilt = Mathf.Sin(chewPhase) * chewRoll * healWeight;
            float chewLift = Mathf.Abs(Mathf.Sin(chewPhase * 2f)) * chewBob * healWeight;

            // 身體：上下起伏、前傾、左右搖、扭腰
            body.localPosition = bodyRestPos + Vector3.up * (bob + breathe + chewLift);
            body.localRotation = bodyRest * Quaternion.Euler(0f, twist, 0f) *
                                 Quaternion.Euler(runLean * moveWeight - 6f * airWeight + lean, 0f, s * waddle * moveWeight);

            // 頭：抵銷一半身體搖擺，保持穩定
            head.localRotation = headRest * Quaternion.Euler(-bob * 60f + chewPitch, 0f, -s * waddle * 0.5f * moveWeight + chewTilt);
            UpdateBone(action == PlayerAction.Heal, c != null ? c.HealProgress : 0f, chewPhase);

            // 腿：前後擺；空中時青蛙腿往前往外張開
            float legAmp = legSwing * moveWeight;
            legR.localRotation = legRRest * Quaternion.Slerp(Quaternion.Euler(s * legAmp, 0f, 0f), Quaternion.Euler(-35f, 0f, 25f), airWeight);
            legL.localRotation = legLRest * Quaternion.Slerp(Quaternion.Euler(-s * legAmp, 0f, 0f), Quaternion.Euler(-35f, 0f, -25f), airWeight);

            // 手臂：和腿反向擺；空中時張開。右臂疊上揮刀姿勢，左臂依序疊上舉盾、盾反推盾、破防
            float armAmp = armSwing * moveWeight;
            Quaternion swingR = Quaternion.Slerp(Quaternion.Euler(-s * armAmp, 0f, 0f), Quaternion.Euler(-20f, 0f, 35f), airWeight);
            Quaternion swingL = Quaternion.Slerp(Quaternion.Euler(s * armAmp, 0f, 0f), Quaternion.Euler(-20f, 0f, -35f), airWeight);
            armR.localRotation = armRRest * Quaternion.Slerp(swingArm * swingR, Quaternion.Euler(healArmRPose), healWeight);
            if (sword != null) sword.localRotation = swordRest * swingWrist;

            Quaternion left = Quaternion.Slerp(swingL, Quaternion.Euler(blockArmPose), blockWeight);
            left = Quaternion.Slerp(left, Quaternion.Euler(healArmLPose), healWeight);
            left = Quaternion.Slerp(left, Quaternion.Euler(parryArmPose), Mathf.Sin(parryTimer / parryPoseTime * Mathf.PI * 0.5f));
            left = Quaternion.Slerp(left, Quaternion.Euler(guardBreakArmPose), guardBreakWeight);
            armL.localRotation = armLRest * left;

            // 盾面朝向在身體空間插值，抵銷左臂的旋轉；破防時盾跟著手臂被撞開
            Quaternion shieldInBody = Quaternion.Slerp(shieldRestInBody, Quaternion.Euler(blockShieldFacing), Mathf.Max(blockWeight, parryTimer > 0f ? 1f : 0f));
            Quaternion shieldLocal = Quaternion.Inverse(armL.localRotation) * shieldInBody;
            shieldGrip.localRotation = Quaternion.Slerp(shieldLocal, Quaternion.Inverse(armLRest) * shieldRestInBody, guardBreakWeight);

            // 尾巴：跑越快搖越快
            if (tail != null)
            {
                float wag = Mathf.Sin(Time.time * Mathf.Lerp(6f, 14f, moveWeight)) * Mathf.Lerp(12f, 20f, moveWeight);
                tail.localRotation = tailRest * Quaternion.Euler(0f, wag, 0f);
            }

            UpdateBlink();
        }

        // 骨頭：開始啃時彈出來，越啃越小，跟著咀嚼左右擺
        void UpdateBone(bool healing, float progress, float chewPhase)
        {
            if (bone == null) return;
            if (bone.gameObject.activeSelf != healing) bone.gameObject.SetActive(healing);
            if (!healing) return;
            float size = healWeight * Mathf.Lerp(1f, boneEatenScale, progress);
            bone.localScale = boneRestScale * size;
            float wiggle = Mathf.Sin(chewPhase * 2f) * boneWiggle;
            bone.localRotation = boneRest * Quaternion.Euler(0f, wiggle, wiggle * 0.5f);
        }

        // 揮刀中照 AttackSpec 的判定時間對齊揮出；蓄力中把刀等速舉到重擊的第一個 key，蓄滿剛好到頂，快滿時抖動
        void ArmPose(PlayerController c, out Quaternion arm, out Quaternion wrist, out float twist, out float lean)
        {
            arm = wrist = Quaternion.identity;
            twist = lean = 0f;
            if (attack != null && attack.IsAttacking)
            {
                AttackPose(attack.Elapsed, attack.Current, KeysFor(attack), out arm, out wrist, out twist);
                if (attack.IsExecution)
                {
                    // 舉刀時微微後仰，砍到後往前傾，收刀時回正
                    var spec = attack.Current;
                    float t = attack.Elapsed;
                    float lift = Mathf.Clamp01(t / Mathf.Max(1e-4f, spec.hitStart));
                    float settle = Mathf.Clamp01((t - spec.hitEnd) / Mathf.Max(1e-4f, spec.duration - spec.hitEnd));
                    lean = t < spec.hitStart ? -executionLean * 0.5f * lift : executionLean * (1f - Mathf.SmoothStep(0f, 1f, settle));
                }
            }
            else if (c != null && c.Action == PlayerAction.Charge && heavy != null && heavy.Length > 0)
            {
                // 等速舉高：先快後慢的曲線會提早舉到頂、停在那裡等蓄滿，感覺卡手
                float k = Mathf.Clamp01((c.ChargeProgress - chargePoseDelay) / (1f - chargePoseDelay));
                float shake = c.ChargeProgress > 0.85f ? Mathf.Sin(Time.time * 70f) * chargeTremble : 0f;
                arm = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(heavy[0].arm), k) * Quaternion.Euler(shake, 0f, 0f);
                wrist = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(heavy[0].wrist), k);
                twist = heavy[0].twist * k;
                lean = chargeLean * k;
            }
            lastArm = arm;
            lastWrist = wrist;
            lastTwist = twist;
        }

        SwingKey[] KeysFor(PlayerAttack a)
        {
            if (a.IsExecution) return execution;
            if (a.IsHeavy) return heavy;
            switch (a.ComboIndex)
            {
                case 0: return forehand;
                case 1: return thrust;
                default: return finisher;
            }
        }

        // 時間軸（秒，從出手算），對齊 AttackSpec 的判定時間：
        // 舉刀（出手時的姿勢 → key0）→ hitStart − swingLeadTime 起加速揮下（key0 → key1）→ hitStart 剛好在 key1（砍到）
        // → hitEnd 揮完（key1 → key2）→ 收刀（key2 → 靜止）。
        // 出手時的姿勢：一般是靜止；重擊是蓄滿的姿勢（已經等於 key0），連擊接段是上一刀的收刀途中
        void AttackPose(float time, in AttackSpec spec, SwingKey[] keys, out Quaternion arm, out Quaternion wrist, out float twist)
        {
            arm = wrist = Quaternion.identity;
            twist = 0f;
            if (keys == null || keys.Length < 3) return;

            float impact = spec.hitStart;
            float swingStart = Mathf.Max(0f, impact - swingLeadTime);
            float follow = Mathf.Max(spec.hitEnd, impact);

            if (time < swingStart)
            {
                float u = time / Mathf.Max(1e-4f, swingStart);
                float k = 1f - (1f - u) * (1f - u);
                arm = Quaternion.Slerp(fromArm, Quaternion.Euler(keys[0].arm), k);
                wrist = Quaternion.Slerp(fromWrist, Quaternion.Euler(keys[0].wrist), k);
                twist = Mathf.Lerp(fromTwist, keys[0].twist, k);
            }
            else if (time < impact)
            {
                float u = (time - swingStart) / Mathf.Max(1e-4f, impact - swingStart);
                BlendKeys(keys[0], keys[1], u * u, out arm, out wrist, out twist); // 越揮越快
            }
            else if (time < follow)
            {
                float u = (time - impact) / Mathf.Max(1e-4f, follow - impact);
                BlendKeys(keys[1], keys[2], 1f - (1f - u) * (1f - u), out arm, out wrist, out twist); // 砍到後減速揮完
            }
            else
            {
                float r = Mathf.SmoothStep(0f, 1f, (time - follow) / Mathf.Max(1e-4f, spec.duration - follow));
                arm = Quaternion.Slerp(Quaternion.Euler(keys[2].arm), Quaternion.identity, r);
                wrist = Quaternion.Slerp(Quaternion.Euler(keys[2].wrist), Quaternion.identity, r);
                twist = keys[2].twist * (1f - r);
            }
        }

        static void BlendKeys(in SwingKey a, in SwingKey b, float k, out Quaternion arm, out Quaternion wrist, out float twist)
        {
            arm = Quaternion.Slerp(Quaternion.Euler(a.arm), Quaternion.Euler(b.arm), k);
            wrist = Quaternion.Slerp(Quaternion.Euler(a.wrist), Quaternion.Euler(b.wrist), k);
            twist = Mathf.Lerp(a.twist, b.twist, k);
        }

        // 受擊後的無敵時間內整隻狗閃爍；倒地後不閃
        void UpdateBlink()
        {
            bool hide = health != null && health.IsHitInvulnerable && !health.IsDead &&
                        Mathf.Repeat(Time.time * invulnerableBlinkRate, 1f) > 0.5f;
            if (hide == blinkHidden) return;
            blinkHidden = hide;
            foreach (var r in renderers)
            {
                if (r != null) r.enabled = !hide;
            }
        }
    }
}
