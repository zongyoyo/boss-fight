using System;
using BossFight.Hub;
using UnityEngine;

namespace BossFight.UI
{
    /// <summary>
    /// 休息區介面的文字與顏色（T20）：互動提示、站點面板（T21–T24 在這裡加各站點的內容）、傳送門的挑戰清單。
    /// HubUIBuilder 建立介面時用版面與顏色，執行時的文字（提示、站點名稱、確認訊息）直接讀資產。資產在 Assets/ScriptableObjects/Hub/HubUIData.asset。
    /// </summary>
    [CreateAssetMenu(fileName = "HubUIData", menuName = "BossFight/Hub/Hub UI Data")]
    public class HubUIData : ScriptableObject
    {
        [Serializable]
        public class StationText
        {
            public HubStationKind kind;
            [Tooltip("互動提示的動詞（按 [E] 之後的字）")]
            public string verb;
            [Tooltip("站點面板的標題")]
            public string title;
        }

        [Header("互動提示")]
        [Tooltip("{0} = 互動鍵，{1} = 站點的動詞")]
        public string promptFormat = "按 [{0}] {1}";
        public StationText[] stations =
        {
            new StationText { kind = HubStationKind.Chief, verb = "對話", title = "村長" },
            new StationText { kind = HubStationKind.Blacksmith, verb = "鍛造", title = "鐵匠鋪" },
            new StationText { kind = HubStationKind.Campfire, verb = "休息", title = "營火" },
            new StationText { kind = HubStationKind.Portal, verb = "出發", title = "選擇挑戰" },
        };
        public Color promptColor = Color.white;
        [Range(10, 80)] public int promptFontSize = 34;
        [Tooltip("提示離畫面下緣多高（Canvas 單位，參考解析度 1920×1080）")]
        public float promptY = 150f;

        [Header("站點面板")]
        [Tooltip("還沒有內容的站點（T21–T24 才做）面板中央的字")]
        public string emptyStationText = "（準備中）";
        [Tooltip("面板下方的返回提示（返回讀的是 UI/Cancel，顯示不出鍵名，所以寫成文字）")]
        public string closeHintKeyboard = "Esc 返回";
        public string closeHintGamepad = "B 返回";
        public Color panelColor = new Color(0.08f, 0.07f, 0.06f, 0.92f);
        public Color titleColor = new Color(1f, 0.85f, 0.6f);
        public Color textColor = Color.white;
        public Color hintColor = new Color(1f, 1f, 1f, 0.6f);
        public Color buttonColor = new Color(0.22f, 0.2f, 0.18f, 1f);
        public Color buttonHighlightColor = new Color(0.45f, 0.36f, 0.24f, 1f);

        [Header("傳送門")]
        [Tooltip("挑戰清單（目前只有 OIIA 貓一列；T24 改讀 BossDefinition）")]
        public string bossLabel = "OIIA 貓";
        [Tooltip("確認訊息：{0} = Boss 名稱")]
        public string departConfirmFormat = "要出發挑戰 {0} 嗎？";
        public string departYes = "出發";
        public string departNo = "取消";

        /// <summary>這個站點的文字；沒有時 null。</summary>
        public StationText Get(HubStationKind kind)
        {
            if (stations == null) return null;
            foreach (var s in stations)
            {
                if (s != null && s.kind == kind) return s;
            }
            return null;
        }
    }
}
