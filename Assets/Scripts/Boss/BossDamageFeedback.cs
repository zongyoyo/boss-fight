using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// Boss 被打到的反饋，讓玩家一眼看出有沒有打中：命中點爆火花、全身閃白（BossTelegraphFlash.FlashHit）、
    /// 身體往後仰一下（OiiaCatPoser.Flinch），頭上跳出實際扣血的數字。
    /// 一般命中白色；頭暈中（受傷倍率 &gt; 1）金色、比較大；處決橘紅色、最大；轉階段無敵時只冒一點灰色火花、不跳數字。
    /// 只訂閱 BossHealth.OnDamaged；顏色、大小、時間都是純視覺參數。
    /// （BossHitFeedback 是反方向：Boss 的招式打到玩家時的火花。）
    /// </summary>
    [DisallowMultipleComponent]
    public class BossDamageFeedback : MonoBehaviour
    {
        /// <summary>一種命中的外觀。</summary>
        [System.Serializable]
        public struct Look
        {
            [ColorUsage(true, true)] public Color spark;
            [Tooltip("火花大小倍率")]
            [Min(0f)] public float sparkSize;
            [Tooltip("全身閃光的顏色（HDR，Emission）；黑色 = 不閃")]
            [ColorUsage(true, true)] public Color flash;
            [Tooltip("往後仰的強度（1 = OiiaCatPoser.flinchAngle）")]
            [Min(0f)] public float flinch;
            public Color number;
            [Tooltip("數字大小倍率；0 = 不跳數字")]
            [Min(0f)] public float numberSize;

            public Look(Color spark, float sparkSize, Color flash, float flinch, Color number, float numberSize)
            {
                this.spark = spark;
                this.sparkSize = sparkSize;
                this.flash = flash;
                this.flinch = flinch;
                this.number = number;
                this.numberSize = numberSize;
            }
        }

        [SerializeField] BossHealth health;
        [SerializeField] BossTelegraphFlash flash;
        [SerializeField] OiiaCatPoser poser;
        [SerializeField] TimedFx sparkPrefab;
        [SerializeField] DamageNumber numberPrefab;
        [Tooltip("數字面向的鏡頭（Main Camera）")]
        [SerializeField] Transform viewCamera;
        [Tooltip("跳傷害數字")]
        [SerializeField] bool showNumbers = true;

        [Header("位置")]
        [Tooltip("火花的高度 (m)，約在刀砍到的高度")]
        [SerializeField] float sparkHeight = 1.2f;
        [Tooltip("火花從 Boss 中心往攻擊者偏移的距離 (m)，大約是身體表面")]
        [Min(0f)] [SerializeField] float sparkOffset = 0.9f;
        [Tooltip("數字出現的高度 (m)，頭頂上方")]
        [SerializeField] float numberHeight = 2.9f;
        [Tooltip("數字左右隨機偏移 (m)，連續命中時才不會疊在一起")]
        [Min(0f)] [SerializeField] float numberJitter = 0.35f;
        [Tooltip("全身閃光的時間（秒）")]
        [Min(0.01f)] [SerializeField] float flashTime = 0.15f;

        [Header("外觀")]
        [SerializeField] Look normal = new Look(new Color(3f, 2.6f, 2f, 1f), 0.8f, new Color(1.2f, 1.2f, 1.2f, 1f), 1f,
                                                Color.white, 1f);
        [Tooltip("頭暈中（受傷倍率 > 1）")]
        [SerializeField] Look stunned = new Look(new Color(3.5f, 2.6f, 0.5f, 1f), 1.2f, new Color(1.8f, 1.4f, 0.3f, 1f), 1.4f,
                                                 new Color(1f, 0.85f, 0.2f, 1f), 1.35f);
        [Tooltip("處決")]
        [SerializeField] Look execution = new Look(new Color(4f, 1.6f, 0.6f, 1f), 1.5f, new Color(2.5f, 1.2f, 0.4f, 1f), 2.5f,
                                                   new Color(1f, 0.45f, 0.2f, 1f), 2f);
        [Tooltip("無敵（轉階段）：打不動")]
        [SerializeField] Look invulnerable = new Look(new Color(0.6f, 0.6f, 0.7f, 1f), 0.5f, Color.black, 0f, Color.gray, 0f);

        void OnEnable()
        {
            if (health != null) health.OnDamaged += HandleDamaged;
        }

        void OnDisable()
        {
            if (health != null) health.OnDamaged -= HandleDamaged;
        }

        void HandleDamaged(BossDamageReport report)
        {
            Look look;
            if (report.result == DamageResult.Evaded) look = invulnerable;
            else if (report.result != DamageResult.Hit) return;
            else if (report.info.isExecution) look = execution;
            else if (report.multiplier > 1f) look = stunned;
            else look = normal;

            Vector3 root = health.transform.position;
            Vector3 toAttacker = report.info.sourcePosition - root;
            toAttacker.y = 0f;
            toAttacker = toAttacker.sqrMagnitude > 1e-4f ? toAttacker.normalized : health.transform.forward;

            Vector3 sparkPos = root + Vector3.up * sparkHeight + toAttacker * sparkOffset;
            TimedFx.Spawn(sparkPrefab, sparkPos, Quaternion.identity, look.spark, look.sparkSize);
            if (flash != null && look.flash.maxColorComponent > 0f) flash.FlashHit(look.flash, flashTime);
            if (poser != null && look.flinch > 0f) poser.Flinch(-toAttacker, look.flinch);

            if (!showNumbers || look.numberSize <= 0f || report.healthLost <= 0f) return;
            Vector3 side = Vector3.Cross(Vector3.up, toAttacker);
            Vector3 numberPos = root + Vector3.up * numberHeight + side * Random.Range(-numberJitter, numberJitter);
            DamageNumber.Spawn(numberPrefab, numberPos, Mathf.RoundToInt(report.healthLost).ToString(), look.number, look.numberSize, viewCamera);
        }
    }
}
