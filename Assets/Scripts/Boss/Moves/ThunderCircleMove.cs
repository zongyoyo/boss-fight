using System.Collections.Generic;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 魔法陣落雷（第一階段，遠距離）：預兆抬頭長叫（紅光），出招時在地上放 circleCount 個紅圈（Hazards/LightningStrike）：
    /// 一個在玩家當下的位置，其他的在玩家周圍隨機；紅圈填滿的那一刻同時落雷。不可擋，要走出紅圈。
    /// 從紅圈出現起 Boss 原地施法、不動，總共 recoveryTime 秒（最後一波落雷後剩下的是破綻）。數值讀 ThunderCircleData。
    /// 追蹤落雷（waveCount &gt; 1，第二階段）：每隔 waveInterval 秒，以玩家當下的位置再放一波同樣排法的紅圈。
    /// </summary>
    [DisallowMultipleComponent]
    public class ThunderCircleMove : BossMove
    {
        [SerializeField] OiiaCatPoser poser;
        [SerializeField] LightningStrike strikePrefab;
        [Tooltip("找不重疊位置的嘗試次數（純效能參數）")]
        [Min(1)] [SerializeField] int placementAttempts = 8;

        ThunderCircleData thunder;
        readonly List<Vector3> points = new List<Vector3>();
        float timer;
        int wavesSpawned;

        /// <summary>recoveryTime 是從第一波紅圈出現算起的施法時間，扣掉到最後一波落雷的時間才是破綻。</summary>
        public override float RecoveryTime => Mathf.Max(0f, Data.recoveryTime - thunder.CastTime);

        void Awake()
        {
            thunder = Data as ThunderCircleData;
            if (thunder == null) Debug.LogError($"[Boss] {name} 的資料不是 ThunderCircleData。", this);
        }

        public override void BeginTelegraph() => poser.SetLookUp(1f);

        public override void BeginActive()
        {
            timer = 0f;
            wavesSpawned = 0;
            SpawnWave();
        }

        void SpawnWave()
        {
            wavesSpawned++;
            PickPoints();
            var damage = CreateDamage();
            foreach (var p in points)
            {
                var strike = SpawnHazard(strikePrefab, p, Quaternion.identity);
                strike.Begin(damage, thunder.circleRadius, thunder.warningTime, thunder.hitWindow, thunder.hitHeight);
            }
        }

        public override bool TickActive(float dt)
        {
            timer += dt;
            if (wavesSpawned < thunder.waveCount && timer >= wavesSpawned * thunder.waveInterval) SpawnWave();
            return timer >= thunder.CastTime;
        }

        public override void End() => poser.ResetPose();

        // 第一個在玩家腳下，其他的在玩家周圍 scatterMin–scatterMax 內，盡量相隔 minSpacing，都離外牆 edgeMargin 以上
        void PickPoints()
        {
            points.Clear();
            float margin = thunder.edgeMargin;
            Vector3 player = Boss.Target != null ? OnGround(Boss.Target.position) : OnGround(Boss.transform.position);
            points.Add(Boss.ClampToArena(player, margin));

            float minGap = thunder.minSpacing;
            for (int i = 1; i < thunder.circleCount; i++)
            {
                Vector3 candidate = player;
                for (int attempt = 0; attempt < placementAttempts; attempt++)
                {
                    float angle = Random.Range(0f, 360f);
                    float distance = Random.Range(thunder.scatterMin, Mathf.Max(thunder.scatterMin, thunder.scatterMax));
                    candidate = Boss.ClampToArena(player + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance, margin);
                    if (FarFromOthers(candidate, minGap)) break;
                }
                points.Add(candidate);
            }
        }

        bool FarFromOthers(Vector3 p, float gap)
        {
            foreach (var q in points)
            {
                if (Flat(p - q).sqrMagnitude < gap * gap) return false;
            }
            return true;
        }
    }
}
