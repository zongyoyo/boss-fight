using UnityEngine;

namespace BossFight.Player
{
    /// <summary>
    /// 刀盾狗的程序化動畫（不用 Animator Controller）：走路擺腿、身體搖擺、舉盾、翻滾、空中收腿、搖尾巴（有尾巴時）。
    /// 只改各關節 pivot 的 localRotation / localPosition，pivot 由 Editor/DaoDunDogBuilder 建立並指派。
    /// 揮刀關鍵姿勢（forehand / thrust / AttackPose）先保留，T2 做連擊時再接上攻擊狀態。
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

        [Header("步伐")]
        [Tooltip("一個完整步伐循環前進的距離 (m)")]
        [Min(0.1f)] public float strideLength = 0.9f;
        public float legSwing = 35f;
        public float armSwing = 10f;
        public float bobHeight = 0.04f;
        [Tooltip("走路時左右搖擺（青蛙腳的鴨子步）")]
        public float waddle = 10f;
        public float runLean = 10f;

        [Header("揮刀關鍵姿勢：蓄力 → 揮到一半 → 揮完（各 3 個）")]
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

        [Header("舉盾")]
        [Tooltip("左臂舉盾姿勢（相對靜止姿勢的 Euler）")]
        public Vector3 blockArmPose = new Vector3(-75f, 40f, 0f);
        [Tooltip("舉盾時盾面在身體空間的朝向（Euler，0 = 正前方）")]
        public Vector3 blockShieldFacing = new Vector3(0f, -5f, 0f);
        [Min(0f)] public float blockBlendSpeed = 12f;

        [Header("翻滾")]
        [Tooltip("翻滾時模型繞這個點（角色局部座標）轉一圈，約在身體中心")]
        public Vector3 rollPivot = new Vector3(0f, 0.6f, 0f);

        Quaternion modelRest, bodyRest, headRest, armRRest, armLRest, legRRest, legLRest, tailRest;
        Quaternion shieldRestInBody;
        Vector3 modelRestPos, bodyRestPos;
        float phase, moveWeight, airWeight, blockWeight;

        void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PlayerController>();
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
            shieldRestInBody = armLRest * shieldGrip.localRotation;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            var c = controller;
            float speed = c != null ? c.PlanarSpeed : 0f;
            float run = c != null ? Mathf.Max(0.01f, c.MoveSpeed) : 1f;
            bool grounded = c == null || c.IsGrounded;
            bool rolling = c != null && c.IsRolling;

            // 空中或翻滾時收腿；翻滾很短，收得快一點
            moveWeight = Mathf.MoveTowards(moveWeight, grounded && !rolling ? Mathf.Clamp01(speed / run) : 0f, dt * 6f);
            airWeight = Mathf.MoveTowards(airWeight, grounded && !rolling ? 0f : 1f, dt * (rolling ? 16f : 8f));
            blockWeight = Mathf.MoveTowards(blockWeight, c != null && c.IsBlocking ? 1f : 0f, dt * blockBlendSpeed);

            phase = Mathf.Repeat(phase + speed / strideLength * dt, 1f);
            float s = Mathf.Sin(phase * 2f * Mathf.PI);
            float bob = Mathf.Abs(Mathf.Cos(phase * 2f * Mathf.PI)) * bobHeight * moveWeight;
            float breathe = Mathf.Sin(Time.time * 2.2f) * 0.012f * (1f - moveWeight);

            // 翻滾：整隻狗繞身體中心往翻滾方向轉一圈（方向可以是側面或後面，跟面向無關）
            if (model != null)
            {
                Quaternion roll = Quaternion.identity;
                if (rolling)
                {
                    Vector3 axis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, c.RollDirection));
                    roll = Quaternion.AngleAxis(360f * Mathf.SmoothStep(0f, 1f, c.RollProgress), axis);
                }
                model.localRotation = roll * modelRest;
                model.localPosition = rollPivot + roll * (modelRestPos - rollPivot);
            }

            // 身體：上下起伏、前傾、左右搖
            body.localPosition = bodyRestPos + Vector3.up * (bob + breathe);
            body.localRotation = bodyRest * Quaternion.Euler(runLean * moveWeight - 6f * airWeight, 0f, s * waddle * moveWeight);

            // 頭：抵銷一半身體搖擺，保持穩定
            head.localRotation = headRest * Quaternion.Euler(-bob * 60f, 0f, -s * waddle * 0.5f * moveWeight);

            // 腿：前後擺；空中時青蛙腿往前往外張開
            float legAmp = legSwing * moveWeight;
            legR.localRotation = legRRest * Quaternion.Slerp(Quaternion.Euler(s * legAmp, 0f, 0f), Quaternion.Euler(-35f, 0f, 25f), airWeight);
            legL.localRotation = legLRest * Quaternion.Slerp(Quaternion.Euler(-s * legAmp, 0f, 0f), Quaternion.Euler(-35f, 0f, -25f), airWeight);

            // 手臂：和腿反向擺；空中時張開
            float armAmp = armSwing * moveWeight;
            Quaternion swingR = Quaternion.Slerp(Quaternion.Euler(-s * armAmp, 0f, 0f), Quaternion.Euler(-20f, 0f, 35f), airWeight);
            Quaternion swingL = Quaternion.Slerp(Quaternion.Euler(s * armAmp, 0f, 0f), Quaternion.Euler(-20f, 0f, -35f), airWeight);
            armR.localRotation = armRRest * swingR;
            armL.localRotation = armLRest * Quaternion.Slerp(swingL, Quaternion.Euler(blockArmPose), blockWeight);

            // 盾面朝向在身體空間插值，抵銷左臂的旋轉
            Quaternion shieldInBody = Quaternion.Slerp(shieldRestInBody, Quaternion.Euler(blockShieldFacing), blockWeight);
            shieldGrip.localRotation = Quaternion.Inverse(armL.localRotation) * shieldInBody;

            // 尾巴：跑越快搖越快
            if (tail != null)
            {
                float wag = Mathf.Sin(Time.time * Mathf.Lerp(6f, 14f, moveWeight)) * Mathf.Lerp(12f, 20f, moveWeight);
                tail.localRotation = tailRest * Quaternion.Euler(0f, wag, 0f);
            }
        }

        // 0–30% 舉刀蓄力（靜止 → key0），30–50% 揮出（key0 → key1 → key2），50–100% 收刀（key2 → 靜止）
        void AttackPose(float t, int index, out Quaternion arm, out Quaternion wrist, out float twist)
        {
            var keys = index == 0 ? forehand : thrust;
            arm = wrist = Quaternion.identity;
            twist = 0f;
            if (keys == null || keys.Length < 3) return;

            if (t < 0.3f)
            {
                float k = 1f - (1f - t / 0.3f) * (1f - t / 0.3f);
                arm = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(keys[0].arm), k);
                wrist = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(keys[0].wrist), k);
                twist = keys[0].twist * k;
            }
            else if (t < 0.5f)
            {
                float k = (t - 0.3f) / 0.2f;
                k *= k;
                int a = k < 0.5f ? 0 : 1;
                float f = k < 0.5f ? k * 2f : k * 2f - 1f;
                arm = Quaternion.Slerp(Quaternion.Euler(keys[a].arm), Quaternion.Euler(keys[a + 1].arm), f);
                wrist = Quaternion.Slerp(Quaternion.Euler(keys[a].wrist), Quaternion.Euler(keys[a + 1].wrist), f);
                twist = Mathf.Lerp(keys[a].twist, keys[a + 1].twist, f);
            }
            else
            {
                float r = Mathf.SmoothStep(0f, 1f, (t - 0.5f) / 0.5f);
                arm = Quaternion.Slerp(Quaternion.Euler(keys[2].arm), Quaternion.identity, r);
                wrist = Quaternion.Slerp(Quaternion.Euler(keys[2].wrist), Quaternion.identity, r);
                twist = keys[2].twist * (1f - r);
            }
        }
    }
}
