using System;
namespace BossFight.Boss.Moves
{
    /// <summary>只發召喚請求；上層負責生成與玩家綁定，Boss 不反向引用 Player 或 Battle。</summary>
    public class CallChildrenMove : GuardianMove
    {
        public event Action OnSummonRequested;
        public int AvailableSlots { get; set; }
        public override bool CanChoose => AvailableSlots > 0;
        public override void BeginActive() { OnSummonRequested?.Invoke(); Impact(BossImpactKind.Summon, Origin); }
        public override bool TickActive(float dt) => true;
    }
}
