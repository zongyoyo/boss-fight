using UnityEngine;

namespace BossFight.Player
{
    /// <summary>
    /// 一刀的數值：傷害、失衡值、時間軸與判定範圍。時間都從出手那一刻算（秒）。
    /// </summary>
    [System.Serializable]
    public struct AttackSpec
    {
        [Min(0f)] public float damage;
        [Tooltip("命中時給目標的失衡值")]
        [Min(0f)] public float imbalance;
        [Tooltip("整個動作的時間（秒）")]
        [Min(0.05f)] public float duration;
        [Tooltip("攻擊判定開始（秒）")]
        [Min(0f)] public float hitStart;
        [Tooltip("攻擊判定結束（秒）")]
        [Min(0f)] public float hitEnd;
        [Tooltip("從這個時間起可以接下一段，或用翻滾、舉盾取消後搖（秒）")]
        [Min(0f)] public float cancelTime;
        [Tooltip("往前踏步的距離 (m)，0 = 不踏步")]
        [Min(0f)] public float lungeDistance;
        [Tooltip("踏步開始（秒）")]
        [Min(0f)] public float lungeStart;
        [Tooltip("踏步結束（秒）")]
        [Min(0f)] public float lungeEnd;
        [Tooltip("判定往前的距離 (m)，從角色中心量起")]
        [Min(0f)] public float reach;
        [Tooltip("判定的寬度 (m)")]
        [Min(0f)] public float width;
    }

    /// <summary>
    /// 刀盾狗的玩法數值（企劃書「玩家角色」一節）。PlayerController、PlayerStamina、PlayerHealth、PlayerAttack、CameraController 都讀這一份。
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

        [Header("跳躍")]
        [Tooltip("跳躍高度 (m)；只能在地面起跳，空中可以移動，但不能攻擊、舉盾、翻滾")]
        [Min(0f)] public float jumpHeight = 1f;
        [Tooltip("跳躍消耗的耐力；0 = 不耗耐力")]
        [Min(0f)] public float jumpStaminaCost = 0f;

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

        [Header("血量")]
        [Min(1f)] public float maxHealth = 100f;
        [Tooltip("被直接打中（沒擋下）後的無敵時間（秒）；格擋、盾反不給無敵")]
        [Min(0f)] public float hitInvulnerableTime = 0.5f;

        [Header("輕攻擊（點擊左鍵，三段連擊）")]
        [Tooltip("依序是第 1、2、3 段；揮刀中再按左鍵，到 cancelTime 就接下一段，沒按就回到第 1 段")]
        public AttackSpec[] lightCombo =
        {
            new AttackSpec
            {
                damage = 10f, imbalance = 3f, duration = 0.5f, hitStart = 0.15f, hitEnd = 0.25f, cancelTime = 0.3f,
                reach = 1.8f, width = 1.6f,
            },
            new AttackSpec
            {
                damage = 10f, imbalance = 3f, duration = 0.5f, hitStart = 0.15f, hitEnd = 0.25f, cancelTime = 0.3f,
                reach = 1.8f, width = 1.6f,
            },
            new AttackSpec
            {
                damage = 16f, imbalance = 3f, duration = 0.7f, hitStart = 0.21f, hitEnd = 0.35f, cancelTime = 0.45f,
                lungeDistance = 0.8f, lungeStart = 0.05f, lungeEnd = 0.3f, reach = 1.8f, width = 1.6f,
            },
        };

        [Header("重攻擊（長按左鍵）")]
        [Tooltip("按住左鍵多久（秒）自動劈出重擊；在這之前放開就是輕攻擊。蓄力中被直接打中會中斷")]
        [Min(0.05f)] public float heavyChargeTime = 0.8f;
        [Tooltip("劈出時消耗的耐力；耐力大於 0 才能劈出，最低扣到 0")]
        [Min(0f)] public float heavyStaminaCost = 30f;
        [Tooltip("蓄滿後的劈砍。蓄力時刀已經舉好，蓄滿就馬上往下劈，hitStart 是刀砍到的時間；太長會覺得卡手")]
        public AttackSpec heavyAttack = new AttackSpec
        {
            damage = 35f, imbalance = 15f, duration = 0.7f, hitStart = 0.1f, hitEnd = 0.22f, cancelTime = 0.45f,
            reach = 2f, width = 2f,
        };

        [Header("攻擊判定")]
        [Tooltip("判定長方體的高度 (m)，從腳底往上")]
        [Min(0.1f)] public float attackHitboxHeight = 1.6f;

        [Header("舉盾")]
        [Tooltip("擋得住的正面角度（度），以角色面向為中心左右各一半")]
        [Range(0f, 360f)] public float blockAngle = 120f;
        [Tooltip("格擋時減少的傷害比例")]
        [Range(0f, 1f)] public float blockDamageReduction = 0.8f;
        [Tooltip("格擋時耐力扣除量 = 原傷害 × 這個倍率")]
        [Min(0f)] public float blockStaminaPerDamage = 1f;
        [Tooltip("舉盾時耐力被打到 0 的破防硬直（秒），期間不能移動、舉盾、攻擊")]
        [Min(0f)] public float guardBreakDuration = 1f;

        [Header("盾反")]
        [Tooltip("命中前這麼多秒內舉盾，且攻擊可盾反，就算盾反")]
        [Min(0f)] public float parryWindow = 0.15f;
        [Tooltip("盾反成功的畫面停頓（秒，真實時間）")]
        [Min(0f)] public float parryHitStop = 0.1f;

        [Header("啃骨頭")]
        [Tooltip("每場可以用的次數")]
        [Min(0)] public int boneCount = 3;
        [Tooltip("一根骨頭回復的血量")]
        [Min(0f)] public float boneHealAmount = 40f;
        [Tooltip("動作時間（秒）：期間逐步回血，不能移動、不可取消，被打也不會中斷；滿血時不能用")]
        [Min(0.05f)] public float boneDuration = 1f;

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
