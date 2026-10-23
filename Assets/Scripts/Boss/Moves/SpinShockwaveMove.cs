using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 旋轉衝擊波（第一階段，近–中距離）：預兆原地越轉越快（紅光），腳下的紅圈慢慢填滿；
    /// 填滿那一刻放出環狀衝擊波（Hazards/ShockwaveRing），波自己往外擴散到外牆，Boss 收轉 spinOutTime 秒後進入破綻。
    /// 不可擋，要翻滾穿過環。招式正常結束時波照常跑完，被打斷時一起收掉。數值讀 SpinShockwaveData。
    /// 雙重衝擊波（waveCount &gt; 1，第二階段）：放完第一圈後繼續高速旋轉，每 waveInterval 秒從 Boss 腳下再放一圈，放完最後一圈才收轉。
    /// </summary>
    [DisallowMultipleComponent]
    public class SpinShockwaveMove : BossMove
    {
        [SerializeField] OiiaCatPoser poser;
        [Tooltip("腳下的提示圈（執行時生成一份）")]
        [SerializeField] GroundTelegraph telegraphPrefab;
        [SerializeField] ShockwaveRing wavePrefab;
        [Tooltip("預兆結束時的轉速（度/秒）")]
        [SerializeField] float spinSpeed = 2160f;
        [Tooltip("預兆開始時的轉速比例")]
        [Range(0f, 1f)] [SerializeField] float startSpinRatio = 0.3f;

        SpinShockwaveData wave;
        GroundTelegraph circle;
        float timer;
        int wavesLeft;

        void Awake()
        {
            wave = Data as SpinShockwaveData;
            if (wave == null) Debug.LogError($"[Boss] {name} 的資料不是 SpinShockwaveData。", this);
            circle = Instantiate(telegraphPrefab);
            circle.name = $"{name}_Telegraph";
        }

        void OnDestroy()
        {
            if (circle != null) Destroy(circle.gameObject);
        }

        public override void BeginTelegraph()
        {
            timer = 0f;
            circle.ShowCircle(OnGround(Boss.transform.position), wave.telegraphRadius, Data.canBlock);
        }

        public override void TickTelegraph(float dt)
        {
            base.TickTelegraph(dt);
            timer += dt;
            float t = TelegraphTime > 0f ? Mathf.Clamp01(timer / TelegraphTime) : 1f;
            poser.SetSpinImmediate(spinSpeed * Mathf.Lerp(startSpinRatio, 1f, t));
            circle.MoveCircle(OnGround(Boss.transform.position), wave.telegraphRadius);
            circle.SetFill(t);
        }

        public override void BeginActive()
        {
            timer = 0f;
            circle.SetFill(1f);
            circle.Strike();
            circle.Hide();
            wavesLeft = wave.waveCount;
            Launch();
        }

        void Launch()
        {
            wavesLeft--;
            Vector3 center = OnGround(Boss.transform.position);
            float endRadius = wave.maxRadius;
            if (endRadius <= 0f && Boss.Arena != null)
            {
                // 擴散到外牆：Boss 不一定在場地中心，要涵蓋到最遠的那一側
                endRadius = Boss.Arena.radius + Flat(center - Boss.ArenaCenter).magnitude;
            }
            var ring = SpawnHazard(wavePrefab, center, Quaternion.identity);
            ring.Launch(CreateDamage(), wave.startRadius, endRadius, wave.expandSpeed, wave.bandWidth, wave.hitHeight);
            Impact(BossImpactKind.Shockwave, center);
        }

        public override bool TickActive(float dt)
        {
            timer += dt;
            if (wavesLeft <= 0) return timer >= wave.spinOutTime;
            if (timer >= wave.waveInterval)
            {
                timer -= wave.waveInterval;
                Launch();
            }
            return false;
        }

        public override void BeginRecovery() => poser.SetSpin(0f);

        public override void End()
        {
            circle.HideImmediate();
            poser.ResetPose();
        }
    }
}
