using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>序章訓練場的版面；Builder 讀取，不在執行期修改。</summary>
    [CreateAssetMenu(fileName = "PrologueLayout", menuName = "BossFight/Progression/Prologue Layout")]
    public class PrologueLayoutData : ScriptableObject
    {
        [Header("空地與出生點")]
        [Tooltip("圓形訓練空地半徑 (m)")]
        [Min(1f)] public float clearingRadius = 15f;
        [Tooltip("南側出生點 (x, z)")]
        public Vector2 playerSpawn = new Vector2(0f, -15f);
        [Tooltip("出生朝向（度），0 = 北")]
        public float playerFacing;
        [Tooltip("菊花的位置 (x, z)")]
        public Vector2 mentorPosition = Vector2.zero;
        [Tooltip("小樹精預留位置 (x, z)")]
        public Vector2 saplingPosition = new Vector2(-2f, -3f);
        [Tooltip("大樹守衛的破土位置 (x, z)")]
        public Vector2 guardianPosition = new Vector2(0f, 9f);
        [Header("樹與邊界")]
        [Tooltip("外圍樹木排數")]
        [Range(2, 3)] public int treeRows = 3;
        [Tooltip("同排樹木間距 (m)")]
        [Min(0.5f)] public float treeSpacing = 2.6f;
        [Tooltip("樹木排距 (m)")]
        [Min(0.5f)] public float rowSpacing = 2.4f;
        [Tooltip("樹幹碰撞半徑 (m)")]
        [Min(0.05f)] public float trunkRadius = 0.3f;
        [Tooltip("隱形牆在空地外的距離 (m)，位於第一排樹後")]
        [Min(0f)] public float wallOffset = 1.2f;
        [Tooltip("隱形牆高度 (m)")]
        [Min(0.1f)] public float wallHeight = 5f;
        [Tooltip("隱形牆厚度 (m)")]
        [Min(0.1f)] public float wallThickness = 1f;

        [Header("佈景（Builder 用固定種子散佈；改完跑 Tools > Prologue > Build Scenery）")]
        [Tooltip("樹、草、地面與遠山的亂數種子")]
        public int visualSeed = 25;
        [Tooltip("佔位樹（沒有正式模型時）的樹高範圍 (m)")]
        public Vector2 treeHeight = new Vector2(6f, 9f);
        [Tooltip("正式樹模型的縮放範圍")]
        public Vector2 treeScale = new Vector2(0.85f, 1.2f);
        [Tooltip("牆外的裝飾樹排數（沒有碰撞，往霧裡淡出）")]
        [Min(0)] public int outerTreeRows = 4;
        [Tooltip("裝飾樹的排距 (m)")]
        [Min(0.5f)] public float outerRowSpacing = 3.5f;
        [Tooltip("裝飾樹的同排間距 (m)")]
        [Min(0.5f)] public float outerTreeSpacing = 4f;
        [Tooltip("裝飾樹位置的隨機偏移 (m)")]
        [Min(0f)] public float outerTreeJitter = 1f;
        [Tooltip("地面網格的半徑 (m)，要蓋到遠山後面")]
        [Min(1f)] public float groundRadius = 90f;
        [Tooltip("地面網格空地內的格距 (m)，往外逐漸放大")]
        [Min(0.5f)] public float groundCellSize = 1.6f;
        [Tooltip("牆外地面起伏的高度 (m)；牆內一律平坦")]
        [Min(0f)] public float groundRelief = 1.2f;
        [Tooltip("往遠山腳下逐漸升高的高度 (m)")]
        [Min(0f)] public float groundRise = 6f;
        [Tooltip("菊花周圍泥土地的半徑 (m)")]
        [Min(0f)] public float earthRadius = 2.6f;
        [Tooltip("空地中央這個半徑內不放草（戰鬥預兆要看得清楚）(m)")]
        [Min(0f)] public float grassInnerRadius = 13f;
        [Tooltip("空地邊緣（grassInnerRadius 到牆）的草叢密度（叢 / m²）")]
        [Min(0f)] public float edgeGrassDensity = 1.2f;
        [Tooltip("牆外林帶的草叢密度（叢 / m²）")]
        [Min(0f)] public float forestGrassDensity = 1f;
        [Tooltip("出生點與教學角色周圍不放草與道具的半徑 (m)")]
        [Min(0f)] public float actorClearance = 2.5f;
        [Tooltip("牆外灌木、石頭、枯枝、樹樁、倒木的密度倍率（1 = Builder 預設）")]
        [Min(0f)] public float propDensity = 1f;
        [Tooltip("遠山環的半徑 (m)，只作背景")]
        [Min(1f)] public float mountainRadius = 70f;
        [Tooltip("遠山數量")]
        [Min(1)] public int mountainCount = 24;

        [Header("天色（Tools > Prologue > Apply Atmosphere 套到開著的序章場景）")]
        [Tooltip("午後陽光的俯角與方位角")]
        public Vector2 sunAngles = new Vector2(42f, -35f);
        [Tooltip("午後陽光顏色")]
        public Color sunColor = new Color(1f, 0.89f, 0.7f);
        [Tooltip("午後陽光強度")]
        [Min(0f)] public float sunIntensity = 1.4f;
        [Tooltip("霧的顏色，也是漸層天空的地平線色（遠景退進天空裡）")]
        public Color fogColor = new Color(0.65f, 0.76f, 0.69f);
        [Tooltip("漸層天空的天頂色")]
        public Color skyTopColor = new Color(0.36f, 0.6f, 0.86f);
        [Tooltip("漸層天空地平線以下的顏色")]
        public Color skyBottomColor = new Color(0.45f, 0.52f, 0.45f);
        [Tooltip("環境光（Trilight）天空色：陰影面偏藍")]
        public Color ambientSky = new Color(0.65f, 0.74f, 0.78f);
        [Tooltip("環境光（Trilight）地平線色")]
        public Color ambientEquator = new Color(0.55f, 0.58f, 0.46f);
        [Tooltip("環境光（Trilight）地面色")]
        public Color ambientGround = new Color(0.25f, 0.3f, 0.2f);
        [Tooltip("午後薄霧濃度")]
        [Min(0f)] public float fogDensity = 0.006f;
    }
}
