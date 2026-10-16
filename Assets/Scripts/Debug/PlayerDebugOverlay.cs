#if UNITY_EDITOR || DEVELOPMENT_BUILD
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Player;
using UnityEngine;

namespace BossFight.DebugTools
{
    /// <summary>
    /// 左上角的玩家狀態，方便驗收 T1、T2：血量、骨頭、耐力與回復速度、水平速度、目前動作（含舉盾秒數、蓄力進度、受擊無敵）、
    /// 上一次翻滾的距離、時間與無敵時間、上一次受擊的結果（命中時已經舉盾幾秒）、上一次出手打到誰、鎖定目標與和 Boss 的水平距離。
    /// 血量、耐力、骨頭、受擊、出手、鎖定、翻滾訂閱事件；速度、動作與無敵每幀讀 PlayerController／PlayerHealth。
    /// 量到的時間以影格為單位，會比設定值多出不到一幀。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerDebugOverlay : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] PlayerStamina stamina;
        [SerializeField] PlayerHealth health;
        [SerializeField] PlayerAttack attack;
        [SerializeField] CameraController cameraController;
        [Tooltip("顯示和這個物件的水平距離（Boss）")]
        [SerializeField] Transform distanceReference;
        [SerializeField] bool visible = true;

        float staminaCurrent, staminaMax, regenRate;
        float healthCurrent, healthMax;
        int bonesLeft, bonesMax;
        LockOnTarget lockTarget;
        Vector3 rollStartPos;
        float rollStartTime, lastRollDistance, lastRollDuration, lastInvulnerableTime;
        bool measuringInvulnerable;
        int rollCount;
        string lastDamageText = "—", lastAttackText = "—";
        int hitCount, blockCount, parryCount, guardBreakCount, evadeCount;
        GUIStyle style;

        void OnEnable()
        {
            if (stamina != null) stamina.OnStaminaChanged += HandleStaminaChanged;
            if (cameraController != null) cameraController.OnLockTargetChanged += HandleLockTargetChanged;
            if (player != null)
            {
                player.OnRollStarted += HandleRollStarted;
                player.OnRollEnded += HandleRollEnded;
            }
            if (health != null)
            {
                health.OnHealthChanged += HandleHealthChanged;
                health.OnBonesChanged += HandleBonesChanged;
                health.OnDamageTaken += HandleDamageTaken;
            }
            if (attack != null) attack.OnAttackHit += HandleAttackHit;
        }

        void OnDisable()
        {
            if (stamina != null) stamina.OnStaminaChanged -= HandleStaminaChanged;
            if (cameraController != null) cameraController.OnLockTargetChanged -= HandleLockTargetChanged;
            if (player != null)
            {
                player.OnRollStarted -= HandleRollStarted;
                player.OnRollEnded -= HandleRollEnded;
            }
            if (health != null)
            {
                health.OnHealthChanged -= HandleHealthChanged;
                health.OnBonesChanged -= HandleBonesChanged;
                health.OnDamageTaken -= HandleDamageTaken;
            }
            if (attack != null) attack.OnAttackHit -= HandleAttackHit;
        }

        void LateUpdate()
        {
            if (measuringInvulnerable && !player.IsInvulnerable)
            {
                lastInvulnerableTime = Time.time - rollStartTime;
                measuringInvulnerable = false;
            }
        }

        void HandleStaminaChanged(float current, float max)
        {
            // 只記沒碰到上限的回復，扣耐力和補到滿的那一幀不算
            if (current > staminaCurrent && current < max && Time.deltaTime > 0f)
                regenRate = (current - staminaCurrent) / Time.deltaTime;
            staminaCurrent = current;
            staminaMax = max;
        }

        void HandleHealthChanged(float current, float max)
        {
            healthCurrent = current;
            healthMax = max;
        }

        void HandleBonesChanged(int left, int max)
        {
            bonesLeft = left;
            bonesMax = max;
        }

        void HandleDamageTaken(PlayerDamageReport r)
        {
            string held = r.blockHeldTime >= 0f ? $"（命中時舉盾 {r.blockHeldTime:0.000} s）" : "";
            string source = r.info.source != null ? r.info.source.name : "?";
            switch (r.result)
            {
                case DamageResult.Parried:
                    parryCount++;
                    lastDamageText = $"<color=#ffe14d>盾反！</color> 免傷{held}";
                    break;
                case DamageResult.Blocked:
                    blockCount++;
                    lastDamageText = $"<color=#9fd8ff>格擋</color> −{r.healthLost:0.0} HP、−{r.staminaLost:0.0} 耐力{held}";
                    break;
                case DamageResult.GuardBroken:
                    guardBreakCount++;
                    lastDamageText = $"<color=#ff9f40>破防</color> −{r.healthLost:0.0} HP、−{r.staminaLost:0.0} 耐力{held}";
                    break;
                case DamageResult.Hit:
                    hitCount++;
                    lastDamageText = $"<color=#ff7070>命中</color> −{r.healthLost:0.0} HP{held}";
                    break;
                case DamageResult.Evaded:
                    evadeCount++;
                    lastDamageText = "<color=#a0ffa0>無敵閃過</color>";
                    break;
                default:
                    return;
            }
            lastDamageText += $"　← {source} {r.info.damage:0}（{(r.info.canBlock ? "可擋" : "不可擋")}）";
        }

        void HandleAttackHit(HitReport r)
        {
            string move = attack.IsHeavy ? "重擊" : $"第 {attack.ComboIndex + 1} 段";
            string target = r.hurtbox != null ? r.hurtbox.transform.root.name : "?";
            lastAttackText = $"{move} → {target}  {r.info.damage:0}（失衡 +{r.info.imbalance:0}）";
        }

        void HandleLockTargetChanged(LockOnTarget target) => lockTarget = target;

        void HandleRollStarted(Vector3 dir)
        {
            rollCount++;
            rollStartPos = player.transform.position;
            // 起滾那一幀的位移已經算進這一幀的 deltaTime
            rollStartTime = Time.time - Time.deltaTime;
            measuringInvulnerable = true;
        }

        void HandleRollEnded()
        {
            Vector3 d = player.transform.position - rollStartPos;
            lastRollDistance = new Vector2(d.x, d.z).magnitude;
            lastRollDuration = Time.time - rollStartTime;
        }

        string StateText()
        {
            string s;
            switch (player.Action)
            {
                case PlayerAction.Roll:
                    s = player.IsInvulnerable ? "<color=#ffd24d>翻滾（無敵）</color>" : "翻滾";
                    break;
                case PlayerAction.Attack:
                    s = attack.IsHeavy ? "重擊" : $"輕攻擊 第 {attack.ComboIndex + 1} 段";
                    if (attack.CanCancel) s += "（可接／可取消）";
                    break;
                case PlayerAction.Charge:
                    s = $"蓄力 {player.ChargeProgress * 100f:0}%";
                    break;
                case PlayerAction.Heal:
                    s = $"啃骨頭 {player.HealProgress * 100f:0}%";
                    break;
                case PlayerAction.GuardBreak:
                    s = $"<color=#ff9f40>破防 {player.GuardBreakRemaining:0.00} s</color>";
                    break;
                case PlayerAction.Dead:
                    s = "<color=#ff4040>倒地（重新進 Play 模式）</color>";
                    break;
                default:
                    if (!player.IsGrounded) s = "空中";
                    else s = player.IsBlocking ? $"舉盾 {player.BlockHeldTime:0.00} s" : "—";
                    break;
            }
            if (health != null && health.IsHitInvulnerable)
                s += $"　<color=#ffd24d>受擊無敵 {health.HitInvulnerableRemaining:0.00} s</color>";
            return s;
        }

        void OnGUI()
        {
            if (!visible || player == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };

            string lockText = lockTarget != null ? $"<color=#ff7070>{lockTarget.name}</color>" : "未鎖定";
            string dist = "—";
            if (distanceReference != null)
            {
                Vector3 d = distanceReference.position - player.transform.position;
                dist = $"{new Vector2(d.x, d.z).magnitude:0.00} m";
            }

            GUILayout.BeginArea(new Rect(10f, 10f, 620f, 330f), GUI.skin.box);
            GUILayout.Label($"血量  {healthCurrent:0.0} / {healthMax:0}　骨頭 {bonesLeft} / {bonesMax}", style);
            GUILayout.Label($"耐力  {staminaCurrent:0.0} / {staminaMax:0}　（回復 {regenRate:0.0}/s）", style);
            GUILayout.Label($"水平速度  {player.PlanarSpeed:0.00} m/s", style);
            GUILayout.Label($"狀態  {StateText()}", style);
            GUILayout.Label($"上次受擊  {lastDamageText}", style);
            GUILayout.Label($"　命中 {hitCount}　格擋 {blockCount}　盾反 {parryCount}　破防 {guardBreakCount}　閃過 {evadeCount}", style);
            GUILayout.Label($"上次出手  {lastAttackText}", style);
            GUILayout.Label($"上次翻滾 #{rollCount}  距離 {lastRollDistance:0.00} m、時間 {lastRollDuration:0.00} s、無敵 {lastInvulnerableTime:0.00} s", style);
            GUILayout.Label($"鎖定  {lockText}", style);
            GUILayout.Label($"與 Boss 水平距離  {dist}", style);
            GUILayout.EndArea();
        }
    }
}
#endif
