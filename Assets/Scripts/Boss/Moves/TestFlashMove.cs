using BossFight.Core;
using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// T3 驗證用的假招式：預兆時原地轉向玩家、閃光（BossTelegraphFlash），出招時以 Boss 為中心打一個球形判定 activeTime 秒，
    /// 然後進入破綻。數值讀 TestFlashMoveData。T4 換成正式招式後可以從 Boss 的招式清單拿掉。
    /// </summary>
    [DisallowMultipleComponent]
    public class TestFlashMove : BossMove
    {
        [Tooltip("攻擊判定，attacker 指向 Boss 根物件")]
        [SerializeField] Hitbox hitbox;

        TestFlashMoveData testData;
        float activeTimer;

        void Awake()
        {
            testData = Data as TestFlashMoveData;
            if (testData == null) Debug.LogError($"[Boss] {name} 的資料不是 TestFlashMoveData。", this);
        }

        void OnEnable()
        {
            hitbox.OnHit += ReportHit;
        }

        void OnDisable()
        {
            hitbox.OnHit -= ReportHit;
        }

        public override void BeginActive()
        {
            activeTimer = 0f;
            hitbox.SetSphere(new Vector3(0f, testData.hitHeight, 0f), testData.hitRadius);
            hitbox.Activate(CreateDamage());
        }

        public override bool TickActive(float dt)
        {
            activeTimer += dt;
            return activeTimer >= testData.activeTime;
        }

        public override void BeginRecovery() => hitbox.Deactivate();

        public override void End() => hitbox.Deactivate();
    }
}
