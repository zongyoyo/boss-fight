using UnityEngine;

namespace BossFight.Player
{
    /// <summary>
    /// 刀盾狗的玩法數值（企劃書「玩家角色」一節）。PlayerController、PlayerStamina、CameraController 都讀這一份。
    /// 執行期只讀不寫；調平衡改 Assets/ScriptableObjects/Player/PlayerStats.asset。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "BossFight/Player Stats")]
    public class PlayerStats : ScriptableObject
    {
        [Header("移動")]
        [Tooltip("跑速 (m/s)，方向相對鏡頭")]
        [Min(0f)] public float moveSpeed = 5f;
        [Tooltip("加減速 (m/s²)")]
        [Min(0.1f)] public float acceleration = 40f;
        [Tooltip("轉身速度 (度/秒)")]
        [Min(0f)] public float turnSpeed = 720f;
        [Tooltip("舉盾時的移動速度倍率")]
        [Range(0f, 1f)] public float blockMoveSpeedMultiplier = 0.4f;
        [Tooltip("重力加速度 (m/s²)，負值往下")]
        public float gravity = -25f;

        [Header("翻滾")]
        [Tooltip("翻滾距離 (m)")]
        [Min(0f)] public float rollDistance = 3f;
        [Tooltip("一次翻滾的總時間（秒）")]
        [Min(0.05f)] public float rollDuration = 0.5f;
        [Tooltip("翻滾開始後的無敵時間（秒）")]
        [Min(0f)] public float rollInvulnerableTime = 0.3f;
        [Tooltip("翻滾消耗的耐力；耐力大於 0 就能翻滾，最低扣到 0")]
        [Min(0f)] public float rollStaminaCost = 25f;
        [Tooltip("翻滾位移曲線：橫軸是時間比例 0..1，縱軸是已移動距離比例 0..1（終點須為 1）")]
        public AnimationCurve rollCurve = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("耐力")]
        [Min(1f)] public float maxStamina = 100f;
        [Tooltip("每秒回復量")]
        [Min(0f)] public float staminaRegen = 30f;
        [Tooltip("舉盾時的每秒回復量")]
        [Min(0f)] public float staminaRegenBlocking = 10f;

        [Header("鎖定")]
        [Tooltip("按下鎖定時，只會選到這個水平距離 (m) 內的目標")]
        [Min(0f)] public float lockOnRange = 20f;

        [Header("視角")]
        [Tooltip("滑鼠靈敏度（度/像素）")]
        [Min(0f)] public float mouseSensitivity = 0.12f;
        [Tooltip("搖桿靈敏度（度/秒）")]
        [Min(0f)] public float stickSensitivity = 200f;
        [Tooltip("上下反轉")]
        public bool invertY;
    }
}
