using UnityEngine;

namespace BossFight.Player
{
    /// <summary>
    /// 刀盾狗的程序化動畫（不用 Animator Controller）：走路擺腿、身體搖擺、揮刀、舉盾、跳躍收腿、搖尾巴（有尾巴時）。
    /// 只改各關節 pivot 的 localRotation / localPosition，pivot 由 Editor/DaoDunDogBuilder 建立並指派。
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

        Quaternion bodyRest, headRest, armRRest, swordRest, armLRest, legRRest, legLRest, tailRest;
        Quaternion shieldRestInBody;
        Vector3 bodyRestPos;
        float phase, moveWeight, airWeight, blockWeight, attackWeight;

        void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            bodyRest = body.localRotation;
            bodyRestPos = body.localPosition;
            headRest = head.localRotation;
            armRRest = armR.localRotation;
            swordRest = sword.localRotation;
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
            float run = c != null ? Mathf.Max(0.01f, c.runSpeed) : 1f;
            float sprint01 = c != null ? Mathf.Clamp01((speed - run) / Mathf.Max(0.01f, c.sprintSpeed - run)) : 0f;
            bool grounded = c == null || c.IsGrounded;
            bool attacking = c != null && c.IsAttacking;

            moveWeight = Mathf.MoveTowards(moveWeight, grounded ? Mathf.Clamp01(speed / run) : 0f, dt * 6f);
            airWeight = Mathf.MoveTowards(airWeight, grounded ? 0f : 1f, dt * 8f);
            blockWeight = Mathf.MoveTowards(blockWeight, c != null && c.IsBlocking ? 1f : 0f, dt * blockBlendSpeed);
            attackWeight = Mathf.MoveTowards(attackWeight, attacking ? 1f : 0f, dt * 20f);

            phase = Mathf.Repeat(phase + speed / strideLength * dt, 1f);
            float s = Mathf.Sin(phase * 2f * Mathf.PI);
            float bob = Mathf.Abs(Mathf.Cos(phase * 2f * Mathf.PI)) * bobHeight * moveWeight;
            float breathe = Mathf.Sin(Time.time * 2.2f) * 0.012f * (1f - moveWeight);

            // 揮刀姿勢與身體扭轉
            float twist = 0f;
            Quaternion attackArm = Quaternion.identity, attackWrist = Quaternion.identity;
            if (attacking) AttackPose(c.AttackProgress, c.AttackIndex, out attackArm, out attackWrist, out twist);

            // 身體：上下起伏、前傾、左右搖、扭腰
            body.localPosition = bodyRestPos + Vector3.up * (bob + breathe);
            body.localRotation = bodyRest * Quaternion.Euler(runLean * moveWeight * (1f + sprint01) - 6f * airWeight,
                                                             twist * attackWeight,
                                                             s * waddle * moveWeight);

            // 頭：抵銷一半身體搖擺，保持穩定
            head.localRotation = headRest * Quaternion.Euler(-bob * 60f, -twist * attackWeight * 0.4f, -s * waddle * 0.5f * moveWeight);

            // 腿：前後擺；空中時青蛙腿往前往外張開
            float legAmp = legSwing * moveWeight * (1f + 0.3f * sprint01);
            legR.localRotation = legRRest * Quaternion.Slerp(Quaternion.Euler(s * legAmp, 0f, 0f), Quaternion.Euler(-35f, 0f, 25f), airWeight);
            legL.localRotation = legLRest * Quaternion.Slerp(Quaternion.Euler(-s * legAmp, 0f, 0f), Quaternion.Euler(-35f, 0f, -25f), airWeight);

            // 手臂：和腿反向擺；空中時張開
            float armAmp = armSwing * moveWeight;
            Quaternion swingR = Quaternion.Slerp(Quaternion.Euler(-s * armAmp, 0f, 0f), Quaternion.Euler(-20f, 0f, 35f), airWeight);
            Quaternion swingL = Quaternion.Slerp(Quaternion.Euler(s * armAmp, 0f, 0f), Quaternion.Euler(-20f, 0f, -35f), airWeight);
            armR.localRotation = armRRest * Quaternion.Slerp(swingR, attackArm, attackWeight);
            sword.localRotation = swordRest * Quaternion.Slerp(Quaternion.identity, attackWrist, attackWeight);
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
