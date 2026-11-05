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

        [Header("魔法陣")]
        [Tooltip("場地中央魔法陣的半徑 (m)；Boss 坐在正中央。T7 轉階段時擴大到場地邊緣")]
        [Min(0.5f)] public float magicCircleRadius = 5f;

        [Header("轉階段")]
        [Tooltip("轉階段最後的無傷害衝擊波把玩家推到離場地中心這個距離 (m)（外圈）；已經在更外面的玩家不移動")]
        [Min(0f)] public float transitionPushRadius = 10f;

        [Header("觀眾席")]
        [Range(0, 40)] public int standTiers = 12;
        [Min(0.3f)] public float tierDepth = 1.2f;
        [Min(0.1f)] public float tierRise = 0.6f;

        [Header("最外圈外牆")]
        [Min(0f)] public float rimWallHeight = 3f;
        [Min(0.3f)] public float rimWallThickness = 1f;

        [Header("外圍地面")]
        [Min(0f)] public float outerGroundMargin = 60f;

        [Header("佈景（暗林，美術 Phase 7）")]
        [Tooltip("開著時競技場是暗林（ArenaSceneryBuilder 放正式模型），不建觀眾席、最外圈外牆與外圍地面；外牆、門與地板的碰撞照舊，只是看不到")]
        public bool forestScenery = true;
        [Tooltip("佈景散佈的亂數種子（純視覺）")]
        public int visualSeed = 7;
        [Tooltip("地面網格的外半徑 (m)")]
        [Min(20f)] public float groundRadius = 70f;
        [Tooltip("地面三角格的邊長 (m)；牆外越往外越大")]
        [Min(0.5f)] public float groundCellSize = 1.6f;
        [Tooltip("牆外地面起伏的幅度 (m)")]
        [Min(0f)] public float groundRelief = 1.6f;
        [Tooltip("地面在最外緣升高多少 (m)，讓遠林像在山坡上")]
        [Min(0f)] public float groundRise = 6f;
        [Tooltip("撞牆圈模組沿圓周的間距 (m)；模組寬約 4–5 m，彼此重疊")]
        [Min(1f)] public float rimSpacing = 3.4f;
        [Tooltip("撞牆圈模組的縮放範圍")]
        public Vector2 rimScale = new Vector2(0.95f, 1.2f);
        [Tooltip("近林（扭曲巨樹與枯樹）的半徑範圍 (m)")]
        public Vector2 nearForest = new Vector2(15.5f, 30f);
        [Tooltip("近林的樹至少相隔 (m)")]
        [Min(1f)] public float nearTreeSpacing = 4.2f;
        [Tooltip("遠林（針葉樹剪影）的半徑範圍 (m)")]
        public Vector2 farForest = new Vector2(30f, 60f);
        [Tooltip("遠林的樹至少相隔 (m)")]
        [Min(1f)] public float farTreeSpacing = 4.6f;
        [Tooltip("樹的縮放範圍")]
        public Vector2 treeScale = new Vector2(0.9f, 1.3f);
        [Tooltip("開場第 1 鏡（Shot1_Overview）在場外繞的俯視角度範圍（從 x 到 y）：離這段路徑太近的樹不種，鏡頭才不會在樹冠裡")]
        public Vector2 introCameraArc = new Vector2(178f, 217f);
        [Tooltip("開場第 1 鏡看得到狗的那段角度（從 x 到 y）：會擋住這段視線的樹不種。鏡頭轉到更南邊時入口的兩棵巨樹本來就擋住狗，那段照種樹")]
        public Vector2 introSightArc = new Vector2(193f, 217f);
        [Tooltip("開場第 1 鏡離場地中心的距離 (m)")]
        [Min(0f)] public float introCameraRadius = 24f;
        [Tooltip("開場第 1 鏡的高度 (m)")]
        [Min(0f)] public float introCameraHeight = 17f;
        [Tooltip("中央半埋石板圈的半徑 (m)，在魔法陣外圈")]
        [Min(0f)] public float slabRingRadius = 5.7f;
        [Tooltip("石板圈的石板數")]
        [Range(0, 48)] public int slabCount = 18;
        [Tooltip("場地裡的水窪數")]
        [Range(0, 30)] public int puddleCount = 7;
        [Tooltip("從場地邊緣往內爬的貼地樹根數")]
        [Range(0, 40)] public int floorRootCount = 12;
        [Tooltip("牆外草叢密度（叢 / m²）")]
        [Min(0f)] public float forestGrassDensity = 0.3f;
        [Tooltip("牆外道具（灌木、石頭、枯枝、樹樁、倒木）的密度倍率")]
        [Min(0f)] public float propDensity = 1f;

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
