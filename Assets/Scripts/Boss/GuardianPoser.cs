using BossFight.Boss.Moves;
using UnityEngine;
namespace BossFight.Boss
{
    /// <summary>逐招的佔位姿勢；只動無碰撞模型，預兆與出手仍由 Boss 事件驅動。</summary>
    public class GuardianPoser : MonoBehaviour
    {
        [SerializeField] BossController boss;
        [SerializeField] BossStagger stagger;
        [SerializeField] Transform crown;
        [SerializeField] Transform[] vines;
        [SerializeField] Transform stars;
        [Header("純視覺")]
        [Min(0.01f), SerializeField] float settleTime = 0.35f;
        [Min(0.01f), SerializeField] float vineModelReach = 5.5f;
        enum Pose { Sweep, Slam, Scatter, Storm, Thrust, Ring, Barrage, Seed, Children }
        Pose pose;
        Vector3 crownScale;
        Vector3[] vineScales;
        float time, warningAge, warningDuration, impactAge = 100f, strikeDuration, reach;
        int strikeIndex;
        bool stunned, warning;
        void Awake()
        {
            crownScale = crown.localScale;
            vineScales = new Vector3[vines.Length];
            for (int i = 0; i < vines.Length; i++) vineScales[i] = vines[i].localScale;
        }
        void OnEnable()
        { boss.OnMoveChosen += Chosen; boss.OnMoveTelegraph += Warning; boss.OnMoveImpact += Impact; boss.OnStateChanged += State; }
        void OnDisable()
        { boss.OnMoveChosen -= Chosen; boss.OnMoveTelegraph -= Warning; boss.OnMoveImpact -= Impact; boss.OnStateChanged -= State; }
        void Chosen(BossMoveChoice choice)
        {
            var move = choice.move;
            pose = move is VineSweepMove ? Pose.Sweep : move is VineSlamMove ? Pose.Slam : move is LeafScatterMove ? Pose.Scatter :
                move is LeafStormMove ? Pose.Storm : move is RootThrustMove ? Pose.Thrust : move is RootRingMove ? Pose.Ring :
                move is SeedBombMove ? Pose.Seed : move is CallChildrenMove ? Pose.Children : Pose.Barrage;
            var data = move.Data as GuardianMoveData;
            strikeDuration = data != null ? data.hitWindow : settleTime;
            reach = data != null ? (pose == Pose.Sweep ? data.radius : data.reach) : vineModelReach;
            strikeIndex = 0; impactAge = settleTime; warningAge = 0f;
        }
        void Warning(MoveTelegraph info) { warning = true; warningAge = 0f; warningDuration = info.duration; }
        void Impact(BossImpact _) { impactAge = 0f; strikeIndex++; warning = false; }
        void State(BossState value)
        {
            stunned = value == BossState.Stunned;
            if (value == BossState.Idle || value == BossState.Recovery || stunned)
            { warning = false; if (value == BossState.Idle || stunned) impactAge = settleTime; }
        }
        void Update()
        {
            float dt = Time.deltaTime; time += dt; impactAge += dt; if (warning) warningAge += dt;
            float charge = warning ? Mathf.SmoothStep(0f, 1f, warningDuration > 0f ? warningAge / warningDuration : 1f) : 0f;
            float swing = Mathf.Clamp01(impactAge / Mathf.Max(strikeDuration, Mathf.Epsilon));
            float release = 1f - Mathf.SmoothStep(0f, 1f, impactAge / settleTime);
            Vector3 crownAngles = new Vector3(Mathf.Sin(time * 1.5f) * 2f, 0f, 0f);
            Vector3 scale = crownScale;
            if (stunned) crownAngles.x = 25f;
            else if (pose == Pose.Scatter)
            {
                crownAngles = new Vector3(-10f * charge, Mathf.Sin(warningAge * 45f) * charge * 8f, release * 10f);
                scale = Vector3.Scale(crownScale, new Vector3(1f + charge * 0.18f + release * 0.12f, 1f - charge * 0.18f, 1f + charge * 0.18f));
            }
            else if (pose == Pose.Storm) crownAngles = new Vector3(-25f * charge + release * 12f, Mathf.Sin(time * 24f) * charge * 6f, 0f);
            else if (pose == Pose.Thrust || pose == Pose.Ring) crownAngles.x += charge * 12f - release * 8f;
            else if (pose == Pose.Barrage) scale = crownScale * (1f + charge * 0.12f);
            else if (pose == Pose.Seed) crownAngles.x = -25f * charge + 30f * release;
            else if (pose == Pose.Children) { scale = crownScale * (1f - charge * 0.15f); crownAngles.z = Mathf.Sin(time * 18f) * charge * 5f; }
            crown.localRotation = Quaternion.Euler(crownAngles); crown.localScale = scale;
            for (int i = 0; i < vines.Length; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 angles = new Vector3(0f, side * 40f, 0f);
                Vector3 vineScale = vineScales[i];
                if (stunned) angles.x = 45f;
                else if (pose == Pose.Sweep && i == vines.Length - 1)
                {
                    if (warning) angles = Vector3.Lerp(angles, new Vector3(-25f, -110f, 25f), charge);
                    else if (release > 0f) angles = Vector3.Lerp(angles, new Vector3(20f, strikeIndex % 2 == 0 ? Mathf.Lerp(180f, -180f, swing) : Mathf.Lerp(-75f, 75f, swing), 0f), release);
                    vineScale.z *= Mathf.Lerp(1f, reach / vineModelReach, Mathf.Max(charge, release));
                }
                else if (pose == Pose.Slam)
                {
                    if (warning || (strikeIndex == 1 && i != 0)) angles = new Vector3(-80f, side * 12f, 0f);
                    if (strikeIndex > 0 && i == (strikeIndex - 1) % vines.Length && release > 0f)
                        angles = new Vector3(Mathf.Lerp(-80f, 55f, swing), side * 12f, 0f);
                    vineScale.z *= Mathf.Lerp(1f, reach / vineModelReach, Mathf.Max(charge, release));
                }
                else if (pose == Pose.Scatter || pose == Pose.Storm) angles = new Vector3(-30f * charge, side * (40f + 40f * charge), 0f);
                else if (pose == Pose.Thrust || pose == Pose.Ring) angles = new Vector3(-50f * charge + 45f * release, side * 25f, 0f);
                else if (pose == Pose.Barrage) angles = new Vector3(-70f * charge + 35f * release, side * 45f, 0f);
                else if (pose == Pose.Seed) angles = new Vector3(-70f * charge + release * 40f, side * 20f, 0f);
                else if (pose == Pose.Children) angles = new Vector3(45f * charge, side * (40f + release * 40f), 0f);
                vines[i].localRotation = Quaternion.Euler(angles); vines[i].localScale = vineScale;
            }
            stars.gameObject.SetActive(stunned); stars.Rotate(Vector3.up, dt * 120f, Space.Self);
        }
    }
}
