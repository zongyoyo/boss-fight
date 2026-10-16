using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 競技場的尺寸與配置。ArenaGenerator 依此建模；Boss 衝撞、安全區、衝擊波等玩法也讀同一份半徑。
    /// 角度一律以俯視計：0 = +Z（北）、90 = +X（東）、180 = -Z（南），順時針。
    /// </summary>
    [CreateAssetMenu(fileName = "ArenaData", menuName = "BossFight/Arena Data")]
    public class ArenaData : ScriptableObject
    {
        [System.Serializable]
        public struct Gate
        {
            public string name;
            [Tooltip("俯視角度：0 = +Z（北）、90 = +X（東）、180 = -Z（南），順時針")]
            public float angle;

            public Gate(string name, float angle)
            {
                this.name = name;
                this.angle = angle;
            }
        }

        [Header("競技區")]
        [Tooltip("可活動範圍的半徑 (m)，量到外牆內側")]
        [Min(1f)] public float radius = 12f;
        [Tooltip("圓周分段數，越高越圓")]
        [Range(16, 256)] public int segments = 128;

        [Header("外牆（碰撞）")]
        [Min(1f)] public float wallHeight = 4f;
        [Min(0.5f)] public float wallThickness = 1.5f;
        [Tooltip("外牆、入口門與門柱的 Layer；Boss 撞牆判定用")]
        public string wallLayer = "ArenaWall";

        [Header("入口")]
        [Min(1f)] public float gateWidth = 3.5f;
        public Gate[] gates = { new Gate("PlayerGate", 180f), new Gate("BossGate", 0f) };

        [Header("出生點")]
        [Tooltip("玩家出生點離場地中心的距離 (m)，在 PlayerGate 的方向上；Boss 固定在中心")]
        [Min(0f)] public float playerSpawnDistance = 8f;

        [Header("觀眾席")]
        [Range(0, 40)] public int standTiers = 12;
        [Min(0.3f)] public float tierDepth = 1.2f;
        [Min(0.1f)] public float tierRise = 0.6f;

        [Header("最外圈外牆")]
        [Min(0f)] public float rimWallHeight = 3f;
        [Min(0.3f)] public float rimWallThickness = 1f;

        [Header("外圍地面")]
        [Min(0f)] public float outerGroundMargin = 60f;

        public float StandsInnerRadius => radius + wallThickness;
        public float StandsOuterRadius => StandsInnerRadius + standTiers * tierDepth;
        public float StandsTopHeight => wallHeight + standTiers * tierRise;
        public float RimOuterRadius => StandsOuterRadius + rimWallThickness;
        public float RimTopHeight => StandsTopHeight + rimWallHeight;

        /// <summary>外牆 Layer 的索引；Layer 不存在時為 -1。</summary>
        public int WallLayerIndex => LayerMask.NameToLayer(wallLayer);
        public LayerMask WallMask => LayerMask.GetMask(wallLayer);

        /// <summary>俯視角度（度）轉成 XZ 平面上的單位方向。0 = +Z，順時針遞增。</summary>
        public static Vector3 Dir(float angleDeg)
        {
            float r = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        public bool TryGetGate(string gateName, out Gate gate)
        {
            if (gates != null)
            {
                foreach (var g in gates)
                {
                    if (g.name == gateName)
                    {
                        gate = g;
                        return true;
                    }
                }
            }
            gate = default;
            return false;
        }
    }
}
