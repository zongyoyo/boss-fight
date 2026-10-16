#if UNITY_EDITOR || DEVELOPMENT_BUILD
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Player;
using UnityEngine;

namespace BossFight.DebugTools
{
    /// <summary>
    /// 左上角的玩家狀態，方便驗收 T1：耐力與回復速度、水平速度、翻滾／無敵／舉盾、
    /// 上一次翻滾的距離、時間與無敵時間、鎖定目標，以及和 Boss 的水平距離。
    /// 耐力、鎖定、翻滾訂閱事件；速度與無敵每幀讀 PlayerController。
    /// 量到的時間以影格為單位，會比設定值多出不到一幀。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerDebugOverlay : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] PlayerStamina stamina;
        [SerializeField] CameraController cameraController;
        [Tooltip("顯示和這個物件的水平距離（Boss）")]
        [SerializeField] Transform distanceReference;
        [SerializeField] bool visible = true;

        float staminaCurrent, staminaMax, regenRate;
        LockOnTarget lockTarget;
        Vector3 rollStartPos;
        float rollStartTime, lastRollDistance, lastRollDuration, lastInvulnerableTime;
        bool measuringInvulnerable;
        int rollCount;
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

        void OnGUI()
        {
            if (!visible || player == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };

            string state = player.IsRolling ? (player.IsInvulnerable ? "<color=#ffd24d>翻滾（無敵）</color>" : "翻滾")
                         : player.IsBlocking ? "舉盾" : "—";
            string lockText = lockTarget != null ? $"<color=#ff7070>{lockTarget.name}</color>" : "未鎖定";
            string dist = "—";
            if (distanceReference != null)
            {
                Vector3 d = distanceReference.position - player.transform.position;
                dist = $"{new Vector2(d.x, d.z).magnitude:0.00} m";
            }

            GUILayout.BeginArea(new Rect(10f, 10f, 460f, 230f), GUI.skin.box);
            GUILayout.Label($"耐力  {staminaCurrent:0.0} / {staminaMax:0}　（回復 {regenRate:0.0}/s）", style);
            GUILayout.Label($"水平速度  {player.PlanarSpeed:0.00} m/s", style);
            GUILayout.Label($"狀態  {state}", style);
            GUILayout.Label($"上次翻滾 #{rollCount}  距離 {lastRollDistance:0.00} m、時間 {lastRollDuration:0.00} s、無敵 {lastInvulnerableTime:0.00} s", style);
            GUILayout.Label($"鎖定  {lockText}", style);
            GUILayout.Label($"與 Boss 水平距離  {dist}", style);
            GUILayout.EndArea();
        }
    }
}
#endif
