using BossFight.Core;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// OIIA 貓這一側的打擊回饋（T11）：預兆音效（白光可擋、紅光不可擋各一種聲音）、招式衝擊（拍下去、撞牆、落地、衝擊波、落雷、龍捲風、大迴旋落地）、
    /// 頭暈與倒下的音效與鏡頭震動。只訂閱同一物件上的 BossController、BossStagger、BossHealth 的事件；
    /// 數值在 CombatFeedbackData，經 FeedbackEmitter 播放（離鏡頭越遠震得越小）。
    /// 玩家打到 Boss 的聲音在玩家那邊（PlayerFeedback）；視覺的火花、閃白在 BossDamageFeedback。
    /// </summary>
    [RequireComponent(typeof(FeedbackEmitter))]
    [DisallowMultipleComponent]
    public class BossImpactFeedback : MonoBehaviour
    {
        [Tooltip("預兆、頭暈、倒下的音效與震動位置：Boss 腳底往上的高度 (m)")]
        [SerializeField] float bodyHeight = 1.2f;

        FeedbackEmitter emitter;
        BossController boss;
        BossStagger stagger;
        BossHealth health;

        CombatFeedbackData Data => emitter.Data;
        Vector3 Body => transform.position + Vector3.up * bodyHeight;

        void Awake()
        {
            emitter = GetComponent<FeedbackEmitter>();
            boss = GetComponent<BossController>();
            stagger = GetComponent<BossStagger>();
            health = GetComponent<BossHealth>();
        }

        void OnEnable()
        {
            if (boss != null)
            {
                boss.OnMoveTelegraph += HandleTelegraph;
                boss.OnMoveImpact += HandleImpact;
            }
            if (stagger != null) stagger.OnStunned += HandleStunned;
            if (health != null) health.OnDefeated += HandleDefeated;
        }

        void OnDisable()
        {
            if (boss != null)
            {
                boss.OnMoveTelegraph -= HandleTelegraph;
                boss.OnMoveImpact -= HandleImpact;
            }
            if (stagger != null) stagger.OnStunned -= HandleStunned;
            if (health != null) health.OnDefeated -= HandleDefeated;
        }

        void HandleTelegraph(MoveTelegraph telegraph)
        {
            if (Data == null) return;
            emitter.Play(telegraph.canBlock ? Data.telegraphBlockable : Data.telegraphUnblockable, Body);
        }

        void HandleImpact(BossImpact impact)
        {
            if (Data == null) return;
            emitter.Play(Cue(impact.kind), impact.position);
        }

        FeedbackCue Cue(BossImpactKind kind)
        {
            switch (kind)
            {
                case BossImpactKind.PawSwipe: return Data.pawSwipe;
                case BossImpactKind.PawFinisher: return Data.pawFinisher;
                case BossImpactKind.WallSlam: return Data.wallSlam;
                case BossImpactKind.PounceLanding: return Data.pounceLanding;
                case BossImpactKind.PhantomLanding: return Data.phantomLanding;
                case BossImpactKind.Shockwave: return Data.shockwave;
                case BossImpactKind.Lightning: return Data.lightning;
                case BossImpactKind.Tornado: return Data.tornado;
                case BossImpactKind.GrandSpinLanding: return Data.grandSpinLanding;
                case BossImpactKind.VineStrike: return Data.vineStrike;
                case BossImpactKind.LeafBlade: return Data.leafBlade;
                case BossImpactKind.RootBurst: return Data.rootBurst;
                case BossImpactKind.SeedBlast: return Data.seedBlast;
                case BossImpactKind.Summon: return Data.summon;
                default: return default;
            }
        }

        void HandleStunned(float duration)
        {
            if (Data != null) emitter.Play(Data.stunned, Body);
        }

        void HandleDefeated()
        {
            if (Data != null) emitter.Play(Data.defeated, Body);
        }
    }
}
