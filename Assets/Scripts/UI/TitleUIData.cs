using UnityEngine;

namespace BossFight.UI
{
    /// <summary>
    /// 標題畫面的文字與版面（T32）：遊戲名、主選單按鈕。按鈕與面板的顏色沿用 HubUIData（跟休息區面板同一套）。
    /// 版面的位置、大小、字級是 TitleBuilder 建立時寫進場景的（改了要重跑 Tools > Title > Build Title）；文字 TitleMenuUI 在 Awake 再套一次。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/UI/Title UI", fileName = "TitleUIData")]
    public class TitleUIData : ScriptableObject
    {
        [Header("遊戲名（左上）")]
        [Tooltip("遊戲名稱（暫定）")]
        public string gameTitle = "刀盾狗大冒險";
        [Range(20, 200)] public int titleFontSize = 120;
        public Color titleColor = new Color(1f, 0.85f, 0.6f);
        [Tooltip("遊戲名的陰影顏色")]
        public Color titleShadowColor = new Color(0f, 0f, 0f, 0.75f);
        [Tooltip("遊戲名左上角離畫面左上角的距離（參考解析度 1920×1080）")]
        public Vector2 titleOffset = new Vector2(140f, 150f);

        [Header("主選單（左下）")]
        public string startLabel = "開始遊戲";
        public string settingsLabel = "設定";
        public string quitLabel = "退出遊戲";
        [Tooltip("按鈕大小")]
        public Vector2 buttonSize = new Vector2(380f, 76f);
        [Tooltip("按鈕之間的間距")]
        [Min(0f)] public float buttonGap = 18f;
        [Range(10, 80)] public int buttonFontSize = 36;
        [Tooltip("最下面一顆按鈕的左下角離畫面左下角的距離（參考解析度 1920×1080）")]
        public Vector2 buttonsOffset = new Vector2(140f, 140f);
    }
}
