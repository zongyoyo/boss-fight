using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 扭屁股飛撲（第一階段，中距離）：預兆伏低扭屁股（白光），地上的落點圈跟著玩家；起跳時鎖定落點，
    /// 拋物線飛 flightTime 秒，落地瞬間打一下圓柱判定，同時落點圈閃亮、出現衝擊環。落點圈在落地那一刻剛好填滿。
    /// 落點停在玩家前方 landShort 公尺，不會走出場地。可擋、可盾反。數值讀 ButtPounceData。
    /// 飛撲本身（伏低、拋物線、落地判定）在 PounceRunner，第二階段的幻影分身也用它。
    /// </summary>
    [DisallowMultipleComponent]
    public class ButtPounceMove : BossMove
    {
        [Tooltip("攻擊判定，attacker 指向 Boss 根物件")]
        [SerializeField] Hitbox hitbox;
        [SerializeField] OiiaCatPoser poser;
        [Tooltip("落點圈（執行時生成一份）")]
        [SerializeField] GroundTelegraph telegraphPrefab;
        [Tooltip("落地的衝擊環")]
        [SerializeField] TimedFx impactPrefab;
        [SerializeField] [ColorUsage(true, true)] Color impactColor = new Color(2.6f, 2.6f, 2.6f, 1f);
        [Tooltip("落地後伏低的程度（0..1）")]
        [Range(0f, 1f)] [SerializeField] float landingCrouch = 0.6f;
        [Tooltip("破綻時伏低的程度（0..1）")]
        [Range(0f, 1f)] [SerializeField] float recoveryCrouch = 0.3f;

        ButtPounceData pounce;
        GroundTelegraph circle;
        PounceRunner runner;

        void Awake()
        {
            pounce = Data as ButtPounceData;
            if (pounce == null) Debug.LogError($"[Boss] {name} 的資料不是 ButtPounceData。", this);
            circle = Instantiate(telegraphPrefab);
            circle.name = $"{name}_Telegraph";
        }

        void Start()
        {
            // Boss 在 BossController.Awake 才綁定
            runner = new PounceRunner(Boss, Boss.transform, poser, hitbox, circle, impactPrefab, impactColor, landingCrouch);
        }

        void OnDestroy()
        {
            if (circle != null) Destroy(circle.gameObject);
        }

        void OnEnable() => hitbox.OnHit += ReportHit;

        void OnDisable() => hitbox.OnHit -= ReportHit;

        public override void BeginTelegraph() => runner.BeginWindup(pounce, TelegraphTime, Data.canBlock);

        public override void TickTelegraph(float dt)
        {
            base.TickTelegraph(dt);
            runner.TickWindup(dt);
        }

        public override void BeginActive() => runner.Leap(CreateDamage());

        public override bool TickActive(float dt) => runner.Tick(dt);

        public override void BeginRecovery()
        {
            hitbox.Deactivate();
            poser.SetCrouch(recoveryCrouch);
        }

        public override void End() => runner.Stop();
    }
}
