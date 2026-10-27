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
        public Vector2 saplingPosition = new Vector2(0f, -3f);
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

        [Header("佔位視覺")]
        [Tooltip("確定性佔位樹木與遠山的種子")]
        public int visualSeed = 25;
        [Tooltip("樹高範圍 (m)")]
        public Vector2 treeHeight = new Vector2(6f, 9f);
        [Tooltip("遠山環的半徑 (m)，只作背景")]
        [Min(1f)] public float mountainRadius = 70f;
        [Tooltip("遠山數量")]
        [Min(1)] public int mountainCount = 24;
        [Tooltip("午後陽光的俯角與方位角")]
        public Vector2 sunAngles = new Vector2(42f, -35f);
        [Tooltip("午後陽光顏色")]
        public Color sunColor = new Color(1f, 0.89f, 0.7f);
        [Tooltip("午後陽光強度")]
        [Min(0f)] public float sunIntensity = 1.4f;
        [Tooltip("霧的顏色")]
        public Color fogColor = new Color(0.65f, 0.76f, 0.69f);
        [Tooltip("午後薄霧濃度")]
        [Min(0f)] public float fogDensity = 0.006f;
        [Tooltip("輕微 Bloom 強度")]
        [Min(0f)] public float bloomIntensity = 0.15f;
    }
}
