using System;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Boss
{
    // 新的動作加在最後
    public enum DaisyAction { Hidden, Emerge, Idle, Burrow, Whip, Slap, PrankBurrow, PrankEmerge, Toss, Peek }
    /// <summary>說話時的動作（純表現，不影響狀態）；新的加在最後，保留資產的編號。</summary>
    public enum DaisyGesture { None, Wave, TiltHead, PatChest, Spin, Point, Clap, Snicker, Surprised, BigGrin, Nod }

    /// <summary>菊花的命令與事件；劇情流程由上層指揮，傷害仍經 Hitbox。</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public class DaisyMentor : MonoBehaviour, IDamageable, IHitReportSource
    {
        [SerializeField] DaisyMentorData data;
        [SerializeField] Hitbox whipHitbox;
        [SerializeField] Collider hurtCollider;
        [Tooltip("T27 接管開場時關閉，改由流程呼叫 Emerge")]
        [SerializeField] bool emergeOnStart = true;

        public event Action<DaisyAction> OnActionChanged;
        public event Action<DaisyAction, float> OnActionProgress;
        public event Action<DaisyAction> OnActionFinished;
        public event Action OnEmerged;
        public event Action OnBurrowed;
        public event Action OnWhipFinished;
        public event Action OnSlapFinished;
        /// <summary>丟出的骨頭飛到目標身上（動作結束）。</summary>
        public event Action OnTossFinished;
        /// <summary>丟骨頭期間每幀送目標位置（骨頭的落點，表現用）。</summary>
        public event Action<Vector3> OnTossTarget;
        /// <summary>說話時的動作；Vector3 是指向的位置（Point 用）。</summary>
        public event Action<DaisyGesture, Vector3> OnGesture;
        public event Action<Vector3> OnSlapTarget;
        public event Action<DaisyAction> OnActionCancelled;
        public event Action<HitReport> OnWhipHit;
        /// <summary>同 OnWhipHit（IHitReportSource：命中火花用）。</summary>
        public event Action<HitReport> OnHitReported;
        public event Action OnPrankRequested;
        /// <summary>菊花開始說一句話（說話聲用）；台詞由上層的對話流程播，這裡只轉成事件。</summary>
        public event Action OnTalked;
        public event Action OnPrankFinished;
        public DaisyAction Action { get; private set; } = DaisyAction.Hidden;
        public DaisyMentorData Data => data;

        Transform target;
        TrainingSapling slapTarget;
        float timer;
        bool impact, prankQueued;
        int hitOpenedFrame;

        void Awake()
        {
            whipHitbox.Filter = OnlyTarget;
            hurtCollider.enabled = false;
        }
        void OnEnable() => whipHitbox.OnHit += HandleWhipHit;
        void OnDisable()
        {
            whipHitbox.OnHit -= HandleWhipHit;
            whipHitbox.Deactivate();
            hurtCollider.enabled = false;
            Action = DaisyAction.Hidden;
            prankQueued = false;
            OnActionChanged?.Invoke(Action);
        }
        void Start()
        {
            OnActionChanged?.Invoke(Action);
            if (emergeOnStart) Emerge();
        }

        /// <summary>首次 Bind 可能早於 Awake；這裡只存引用。</summary>
        public void SetTarget(Transform value) => target = value;
        bool OnlyTarget(Collider collider) => target != null && collider.transform.IsChildOf(target);
        /// <summary>上層播菊花的一句台詞時呼叫。</summary>
        public void Talk() => OnTalked?.Invoke();
        /// <summary>說話時的動作（純表現）；point 是 Point 指向的位置。藏在土裡時不做。</summary>
        public void Gesture(DaisyGesture gesture, Vector3 point = default)
        {
            if (Action == DaisyAction.Hidden) return;
            OnGesture?.Invoke(gesture, point);
        }

        void HandleWhipHit(HitReport report)
        {
            OnWhipHit?.Invoke(report);
            OnHitReported?.Invoke(report);
        }

        public bool Emerge()
        {
            if (Action != DaisyAction.Hidden) return false;
            Begin(DaisyAction.Emerge);
            return true;
        }
        /// <summary>停止目前演出與待執行動作；不發完成事件。</summary>
        public void CancelActions()
        {
            OnActionCancelled?.Invoke(Action);
            whipHitbox.Deactivate(); hurtCollider.enabled = false;
            prankQueued = false; slapTarget = null; impact = false;
            Action = DaisyAction.Hidden; timer = 0f; OnActionChanged?.Invoke(Action);
        }
        public bool Burrow()
        {
            if (Action == DaisyAction.Hidden || Action == DaisyAction.Burrow) return false;
            if (Action != DaisyAction.Idle) OnActionCancelled?.Invoke(Action);
            prankQueued = false;
            Begin(DaisyAction.Burrow);
            return true;
        }
        public bool Whip()
        {
            if (Action != DaisyAction.Idle || target == null) return false;
            Begin(DaisyAction.Whip);
            return true;
        }
        /// <summary>把骨頭丟給目標；骨頭飛到時送 OnTossFinished。</summary>
        public bool TossBone()
        {
            if (Action != DaisyAction.Idle || target == null) return false;
            Begin(DaisyAction.Toss);
            return true;
        }
        /// <summary>還藏在土裡時讓土堆動一下（冒出一點再縮回）。</summary>
        public bool Peek()
        {
            if (Action != DaisyAction.Hidden) return false;
            Begin(DaisyAction.Peek);
            return true;
        }
        public bool Slap(TrainingSapling sapling)
        {
            if (Action != DaisyAction.Idle || sapling == null || !sapling.IsAlive) return false;
            slapTarget = sapling;
            Begin(DaisyAction.Slap);
            return true;
        }

        void Begin(DaisyAction action)
        {
            whipHitbox.Deactivate();
            Action = action;
            timer = 0f;
            impact = false;
            hurtCollider.enabled = action == DaisyAction.Idle || action == DaisyAction.Whip || action == DaisyAction.Slap || action == DaisyAction.Toss;
            OnActionChanged?.Invoke(action);
            OnActionProgress?.Invoke(action, 0f);
        }

        void Update()
        {
            if (Time.deltaTime <= 0f || Action == DaisyAction.Hidden) return;
            Transform facing = Action == DaisyAction.Slap && slapTarget != null ? slapTarget.transform : target;
            if (facing != null)
            {
                Vector3 to = facing.position - transform.position; to.y = 0f;
                if (to.sqrMagnitude > 1e-6f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), data.turnSpeed * Time.deltaTime);
            }
            if (Action == DaisyAction.Idle) return;
            timer += Time.deltaTime;
            if (Action == DaisyAction.Whip && !impact && timer >= data.whipImpactTime && target != null)
            {
                impact = true;
                hitOpenedFrame = Time.frameCount;
                whipHitbox.SetSphere(Vector3.zero, data.hitRadius);
                whipHitbox.Activate(new DamageInfo(data.whipDamage, false, false, 0f,
                    ignoreInvulnerability: true, ignoreDefense: true, pushDistance: data.pushDistance, pushDuration: data.pushDuration));
            }
            if (Action == DaisyAction.Whip && whipHitbox.IsActive)
            {
                // 玩家 Update 之後對準實際位置；劇情鎖定與 Debug 翻滾都能確實抽中一次。
                if (target != null) whipHitbox.transform.position = target.position + Vector3.up * data.targetHeight;
                if (timer >= data.whipImpactTime + data.hitWindow && Time.frameCount > hitOpenedFrame) whipHitbox.Deactivate();
            }
            if (Action == DaisyAction.Slap && !impact && timer >= data.slapImpactTime)
            {
                impact = true;
                if (slapTarget != null) slapTarget.ForceStun();
            }
            if (Action == DaisyAction.Slap && slapTarget != null) OnSlapTarget?.Invoke(slapTarget.transform.position);
            if (Action == DaisyAction.Toss && target != null) OnTossTarget?.Invoke(target.position);
            float duration = Duration(Action);
            OnActionProgress?.Invoke(Action, duration > 0f ? Mathf.Clamp01(timer / duration) : 1f);
            if (timer >= duration && !(Action == DaisyAction.Whip && whipHitbox.IsActive)) Complete();
        }

        float Duration(DaisyAction action)
        {
            switch (action)
            {
                case DaisyAction.Emerge: case DaisyAction.PrankEmerge: return data.emergeTime;
                case DaisyAction.Burrow: case DaisyAction.PrankBurrow: return data.burrowTime;
                case DaisyAction.Whip: return Mathf.Max(data.whipTime, data.whipImpactTime + data.hitWindow);
                case DaisyAction.Slap: return Mathf.Max(data.slapTime, data.slapImpactTime);
                case DaisyAction.Toss: return data.tossTime;
                case DaisyAction.Peek: return data.peekTime;
                default: return 0f;
            }
        }

        void Complete()
        {
            DaisyAction completed = Action;
            if (completed == DaisyAction.PrankBurrow) Begin(DaisyAction.PrankEmerge);
            else Begin(completed == DaisyAction.Burrow || completed == DaisyAction.Peek ? DaisyAction.Hidden : DaisyAction.Idle);
            OnActionFinished?.Invoke(completed);
            switch (completed)
            {
                case DaisyAction.Emerge: OnEmerged?.Invoke(); break;
                case DaisyAction.Burrow: OnBurrowed?.Invoke(); break;
                case DaisyAction.Whip: OnWhipFinished?.Invoke(); break;
                case DaisyAction.Slap: OnSlapFinished?.Invoke(); break;
                case DaisyAction.Toss: OnTossFinished?.Invoke(); break;
                case DaisyAction.PrankEmerge: OnPrankFinished?.Invoke(); break;
            }
            if (prankQueued && Action == DaisyAction.Idle) { prankQueued = false; Begin(DaisyAction.PrankBurrow); }
        }

        public DamageOutcome TakeDamage(in DamageInfo info)
        {
            if (!hurtCollider.enabled) return DamageResult.Ignored;
            if (!prankQueued)
            {
                prankQueued = true;
                OnPrankRequested?.Invoke();
            }
            // 關鍵劇情動作先做完，避免玩家打菊花時吞掉藤蔓或巴掌完成事件。
            if (Action == DaisyAction.Idle) { prankQueued = false; Begin(DaisyAction.PrankBurrow); }
            return DamageResult.Evaded;
        }
    }
}
