using UnityEngine;

namespace BossFight.UI
{
    /// <summary>
    /// 標題畫面的文字與版面（T32、T33）：遊戲名、主選單、存檔欄位面板、刪除確認。按鈕與面板的顏色沿用 HubUIData（跟休息區面板同一套），
    /// 返回提示沿用 PauseMenuData 的 navigationHint。
    /// 版面的位置、大小、字級是 TitleBuilder 建立時寫進場景的（改了要重跑 Tools > Title > Build Title）；文字 TitleMenuUI 在執行時讀。
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

        [Header("存檔欄位面板（左側）")]
        [Tooltip("面板標題")]
        public string slotsTitle = "選擇存檔";
        [Tooltip("欄位名稱；{0} = 第幾格（1 起算）")]
        public string slotNameFormat = "存檔 {0}";
        [Tooltip("空欄位的說明")]
        public string emptySlotText = "空存檔";
        [Tooltip("壞檔的說明（載入時會備份壞檔、從新存檔開始）")]
        public string corruptSlotText = "存檔讀取失敗，載入後會從新存檔開始";
        [Tooltip("有存檔時的摘要；{0} = 角色、{1} = 進度、{2} = 最後存檔時間")]
        public string slotSummaryFormat = "{0}　｜　{1}　｜　{2}";
        [Tooltip("還在序章（序章沒完成）的進度文字")]
        public string progressPrologue = "序章";
        [Tooltip("序章完成後的進度文字；{0} = 擊敗過的 Boss 數")]
        public string progressHubFormat = "休息區・擊敗 {0} 隻 Boss";
        [Tooltip("最後存檔時間的格式（.NET DateTime 格式字串）")]
        public string timeFormat = "yyyy/MM/dd HH:mm";
        [Tooltip("沒有存檔時間（T31 以前的存檔）時顯示")]
        public string noTimeText = "—";
        [Tooltip("載入（有存檔的欄位）")]
        public string loadLabel = "載入";
        [Tooltip("新遊戲（空欄位時取代「載入」）")]
        public string newGameLabel = "新遊戲";
        [Tooltip("刪除（空欄位時不能按）")]
        public string deleteLabel = "刪除";
        public string backLabel = "返回";
        [Tooltip("面板大小")]
        public Vector2 slotsPanelSize = new Vector2(820f, 620f);
        [Tooltip("面板左下角離畫面左下角的距離")]
        public Vector2 slotsPanelOffset = new Vector2(110f, 90f);
        [Tooltip("一格存檔欄位的大小")]
        public Vector2 slotSize = new Vector2(740f, 90f);
        [Tooltip("欄位之間的間距")]
        [Min(0f)] public float slotGap = 10f;
        [Range(10, 80)] public int slotNameFontSize = 34;
        [Range(10, 80)] public int slotSummaryFontSize = 24;
        [Tooltip("選取中的欄位疊上的顏色（點一下選取，再點一下載入）")]
        public Color chosenSlotColor = new Color(0.62f, 0.45f, 0.24f, 0.75f);
        [Tooltip("載入、刪除、返回按鈕的大小")]
        public Vector2 actionButtonSize = new Vector2(220f, 70f);

        [Header("刪除確認")]
        [Tooltip("{0} = 第幾格（1 起算）")]
        public string deleteConfirmFormat = "刪除存檔 {0}？\n這一格的所有進度都會消失，無法復原。";
        public string confirmYes = "刪除";
        public string confirmNo = "取消";
        [Tooltip("確認框後面的暗幕")]
        public Color dimColor = new Color(0f, 0f, 0f, 0.6f);
    }
}
