using System;
using UnityEngine;

namespace BossFight.UI
{
    /// <summary>ESC 選單秘技的一個輸入：鍵盤是方向鍵與 A、B 字母鍵，手把是十字鍵與 A（下）、B（右）鍵。</summary>
    public enum MenuCodeInput
    {
        Up,
        Down,
        Left,
        Right,
        A,
        B,
    }

    /// <summary>
    /// ESC 選單的文字、顏色、改鍵清單、試聽音效、難度頁的文字與解鎖開發者模式的秘技。PauseMenuUI、KeyBindingPageUI 執行時讀這一份；
    /// 頁面標題、按鈕上的固定文字與按鈕顏色由 PauseMenuBuilder 建立時寫進場景，改了要重跑 Tools > UI > Build Pause Menu。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Pause Menu Data", fileName = "PauseMenuData")]
    public class PauseMenuData : ScriptableObject
    {
        /// <summary>改鍵頁的一列。</summary>
        [Serializable]
        public class BindingEntry
        {
            [Tooltip("動作路徑，例如 Player/Attack")]
            public string action;
            [Tooltip("組合鍵的部分（Move 的 up／down／left／right）；一般動作留空")]
            public string compositePart;
            public string label;
            [Tooltip("出現在鍵鼠頁")]
            public bool keyboard = true;
            [Tooltip("出現在控制器頁")]
            public bool gamepad = true;
            [Tooltip("改鍵時和其他動作同鍵就互換；長按跳過開場本來就和跳躍同鍵，不檢查")]
            public bool checkConflict = true;

            public BindingEntry() { }

            public BindingEntry(string action, string compositePart, string label, bool keyboard, bool gamepad, bool checkConflict)
            {
                this.action = action;
                this.compositePart = compositePart;
                this.label = label;
                this.keyboard = keyboard;
                this.gamepad = gamepad;
                this.checkConflict = checkConflict;
            }
        }

        [Header("改鍵清單（由上到下）")]
        public BindingEntry[] bindings =
        {
            new BindingEntry("Player/Move", "up", "前進", true, false, true),
            new BindingEntry("Player/Move", "down", "後退", true, false, true),
            new BindingEntry("Player/Move", "left", "左移", true, false, true),
            new BindingEntry("Player/Move", "right", "右移", true, false, true),
            new BindingEntry("Player/Attack", "", "攻擊（長按重擊）", true, true, true),
            new BindingEntry("Player/Block", "", "舉盾", true, true, true),
            new BindingEntry("Player/Roll", "", "翻滾", true, true, true),
            new BindingEntry("Player/Jump", "", "跳躍", true, true, true),
            new BindingEntry("Player/LockOn", "", "鎖定", true, true, true),
            new BindingEntry("Player/Heal", "", "啃骨頭", true, true, true),
            new BindingEntry("Player/Execute", "", "處決", true, true, true),
            new BindingEntry("Player/SkipCutscene", "", "長按跳過開場", true, true, false),
        };

        [Header("主選單")]
        public string pauseTitle = "暫停";
        public string resumeLabel = "繼續";
        public string settingsLabel = "設定";
        public string retryLabel = "重新挑戰";
        public string quitLabel = "退出遊戲";
        [Tooltip("主選單的按鈕與難度頁的標題")]
        public string difficultyLabel = "難度";

        [Header("設定")]
        public string controlsLabel = "按鍵設定";
        public string volumeLabel = "音量設定";
        [Tooltip("{0} = onText／offText")]
        public string developerModeFormat = "開發者模式：{0}";
        public string onText = "開";
        public string offText = "關";
        public string backLabel = "返回";
        [Tooltip("每一頁下方的操作提示（用鍵鼠時）")]
        public string navigationHintKeyboard = "Esc 返回";
        [Tooltip("每一頁下方的操作提示（用手把時）")]
        public string navigationHintGamepad = "B 返回";

        [Header("按鍵設定")]
        public string keyboardLabel = "鍵鼠按鍵設定";
        public string gamepadLabel = "控制器按鍵設定";
        public string resetLabel = "恢復預設";
        public string actionHeader = "動作";
        public string primaryHeader = "主要";
        public string secondaryHeader = "次要";
        public string gamepadHeader = "按鍵";
        [Tooltip("鍵鼠頁固定不能改的操作")]
        public string keyboardFixedNote = "視角：滑鼠　　選單：Esc";
        [Tooltip("控制器頁固定不能改的操作")]
        public string gamepadFixedNote = "移動：左搖桿　　視角：右搖桿　　選單：Start";
        public string keyboardHint = "點一格後按下新的按鍵或滑鼠鍵";
        public string gamepadHint = "選一格後按下新的手把按鍵";
        [Tooltip("等待按鍵時格子裡的文字")]
        public string listeningText = "請按下按鍵…";
        public string keyboardListeningHint = "按下新的按鍵　Esc 取消";
        public string keyboardListeningHintSecondary = "按下新的按鍵　Esc 取消　Delete 清除";
        public string gamepadListeningHint = "按下新的手把按鍵　Start／Esc 取消";
        [Tooltip("{0} = 另一個動作")]
        public string swappedFormat = "已和「{0}」互換";
        [Tooltip("次要欄原本空著、新鍵是另一個動作的次要鍵：{0} = 另一個動作")]
        public string movedFormat = "已從「{0}」移過來";
        [Tooltip("次要欄原本空著、新鍵是另一個動作的主要鍵：不換，{0} = 另一個動作")]
        public string inUseFormat = "「{0}」的主要鍵正在用這個鍵";
        public string resetDoneText = "已恢復預設";

        [Header("音量設定")]
        public string masterLabel = "主音量";
        public string musicLabel = "音樂";
        public string sfxLabel = "音效";
        [Tooltip("{0} = 0..100")]
        public string percentFormat = "{0}%";
        [Tooltip("調音效音量時播的試聽音效")]
        public AudioClip sfxPreview;
        [Range(0f, 1f)] public float sfxPreviewVolume = 0.8f;
        [Tooltip("試聽最短間隔（秒，真實時間）：拖動滑桿時不會連續響")]
        [Min(0f)] public float sfxPreviewInterval = 0.2f;

        [Header("難度")]
        public string hardLabel = "困難";
        public string easyLabel = "簡單";
        [Tooltip("目前難度的按鈕；{0} = hardLabel／easyLabel")]
        public string currentDifficultyFormat = "{0}（目前）";
        [Tooltip("難度頁按鈕下方的說明")]
        [TextArea(4, 12)]
        public string difficultyDescription =
            "困難：原本的難度。\n" +
            "簡單（相對於困難）：\n" +
            "翻滾無敵 +0.1 秒　　盾反判定 +0.05 秒\n" +
            "OIIA 失衡值累積 +15%　　失衡值衰減 −15%\n" +
            "OIIA 出招預兆 +0.15 秒　　貓掌第一下蓄力 +0.2 秒\n" +
            "OIIA 龍捲風速度 −10%　　雙重衝擊波間隔 +0.1 秒";
        [Tooltip("選了另一個難度時的確認訊息；{0} = hardLabel／easyLabel")]
        public string difficultyConfirmFormat = "切換為{0}模式？\n切換難度會重新挑戰，這場戰鬥的進度會消失。";

        [Header("秘技：解鎖開發者模式")]
        [Tooltip("選單開著時依序輸入就會在「設定」顯示開發者模式開關（這次執行有效，不存檔）。輸入到第一個 A／B 之前，手把的 A、B 暫時不會按下按鈕或返回")]
        public MenuCodeInput[] developerUnlockCode =
        {
            MenuCodeInput.Up, MenuCodeInput.Up, MenuCodeInput.Down, MenuCodeInput.Down,
            MenuCodeInput.Left, MenuCodeInput.Left, MenuCodeInput.Right, MenuCodeInput.Right,
            MenuCodeInput.A, MenuCodeInput.B,
        };
        [Tooltip("解鎖時選單下方顯示的提示")]
        public string developerUnlockedText = "已解鎖開發者模式（設定）";
        [Tooltip("提示顯示幾秒（真實時間），最後 noticeFadeTime 秒淡出")]
        [Min(0f)] public float noticeTime = 2.5f;
        [Min(0.01f)] public float noticeFadeTime = 0.5f;
        [Tooltip("解鎖時播的音效")]
        public AudioClip developerUnlockSfx;
        [Range(0f, 1f)] public float developerUnlockVolume = 0.9f;

        [Header("確認")]
        public string retryConfirm = "重新挑戰？\n這場戰鬥的進度會消失。";
        public string quitConfirm = "退出遊戲？";
        public string confirmYes = "確定";
        public string confirmNo = "取消";

        [Header("顏色")]
        [Tooltip("選單後面的暗幕")]
        public Color dimColor = new Color(0f, 0f, 0f, 0.6f);
        public Color panelColor = new Color(0.08f, 0.08f, 0.11f, 1f);
        public Color titleColor = new Color(1f, 0.85f, 0.4f);
        public Color textColor = Color.white;
        [Tooltip("提示、表頭")]
        public Color hintColor = new Color(1f, 1f, 1f, 0.6f);
        public Color buttonColor = new Color(0.2f, 0.2f, 0.24f, 0.95f);
        [Tooltip("滑鼠移上去、鍵盤／手把選到")]
        public Color buttonHighlightColor = new Color(0.45f, 0.38f, 0.2f, 1f);
        public Color buttonPressedColor = new Color(0.7f, 0.58f, 0.25f, 1f);
        [Tooltip("等待按鍵中的格子文字")]
        public Color listeningColor = new Color(1f, 0.85f, 0.4f);
        [Tooltip("改鍵結果（互換、移過來、不換）")]
        public Color noticeColor = new Color(0.55f, 1f, 0.95f);
        public Color sliderFillColor = new Color(0.98f, 0.8f, 0.45f);
    }
}
