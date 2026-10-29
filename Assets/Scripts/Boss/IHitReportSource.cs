using System;
using BossFight.Core;

namespace BossFight.Boss
{
    /// <summary>
    /// 會攻擊玩家的角色（OIIA 貓、大樹守衛、小樹精、菊花）共用的結算回報：攻擊判定打到目標並結算完就送一次。
    /// 命中火花（BossHitFeedback）只認這個介面，不為個別角色開特例。
    /// </summary>
    public interface IHitReportSource
    {
        event Action<HitReport> OnHitReported;
    }
}
