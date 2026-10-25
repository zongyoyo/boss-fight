using System;
using BossFight.Hub;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.UI
{
    /// <summary>
    /// 休息區介面的文字與顏色（T20）：互動提示、站點面板（T21–T24 在這裡加各站點的內容）、營火（T21）、鐵匠鋪（T22）、傳送門的挑戰清單。
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

        [Header("營火")]
        [Tooltip("頁籤的字")]
        public string campfireCharactersTab = "角色";
        public string campfireSkillTreeTab = "技能樹";
        [Tooltip("角色清單裡目前角色那一列：{0} = 角色名稱")]
        public string currentCharacterFormat = "{0}（目前）";
        [Tooltip("目前角色那一列的字色")]
        public Color currentCharacterColor = new Color(1f, 0.85f, 0.6f);
        [Tooltip("目前這一頁的頁籤底色")]
        public Color tabActiveColor = new Color(0.62f, 0.45f, 0.24f, 1f);
        [Tooltip("角色清單每列的間距（Canvas 單位）")]
        [Min(0f)] public float characterRowSpacing = 14f;

        [Serializable]
        public class StatLabel
        {
            public PlayerStatId stat;
            [Tooltip("顯示名稱")]
            public string label;
            [Tooltip("數字格式（ToString）")]
            public string format = "0.##";
            [Tooltip("數值變小才是變好（例如翻滾耐力消耗）")]
            public bool lowerIsBetter;
        }

        [Header("鐵匠鋪")]
        [Tooltip("左邊欄位上方：{0} = 角色名稱")]
        public string blacksmithCharacterFormat = "{0}的裝備";
        [Tooltip("欄位空著時的字")]
        public string emptySlotText = "（空）";
        [Tooltip("選到空欄位時下方的說明")]
        public string emptySlotHint = "從右邊選一件裝備穿上。";
        [Tooltip("未解鎖裝備的名稱")]
        public string lockedEquipmentName = "？？？";
        [Tooltip("未解鎖裝備的來源提示：{0} = EquipmentData.sourceHint")]
        public string equipmentSourceFormat = "來源：{0}";
        [Tooltip("目前角色沒有放得下的欄位：{0} = 角色名稱，{1} = 欄位種類")]
        public string noSlotFormat = "{0}沒有{1}欄，不能裝備。";
        public string newBadgeText = "新";
        public string equippedTagText = "裝備中";
        [Tooltip("差異預覽的開頭")]
        public string equipPreviewHeader = "穿上後：";
        public string unequipPreviewHeader = "卸下後：";
        public string noStatChangeText = "數值沒有變化";
        [Tooltip("數值差異的一行：{0} = 名稱，{1} = 換裝前，{2} = 換裝後")]
        public string statChangeFormat = "{0}　{1} → {2}";
        [Tooltip("加上、拿掉的效果：{0} = PlayerEffect.displayName")]
        public string effectAddedFormat = "＋ {0}";
        public string effectRemovedFormat = "－ {0}";
        [Tooltip("Debug（開發者模式）按鈕")]
        public string debugUnlockAllLabel = "解鎖全部裝備（Debug）";
        [Tooltip("數值變好、變差的顏色")]
        public Color betterColor = new Color(0.45f, 0.95f, 0.45f);
        public Color worseColor = new Color(1f, 0.42f, 0.38f);
        [Tooltip("未解鎖裝備的剪影顏色")]
        public Color lockedIconColor = new Color(0f, 0f, 0f, 0.85f);
        [Tooltip("目前角色穿不了的裝備：圖示與名稱的不透明度")]
        [Range(0f, 1f)] public float unavailableAlpha = 0.35f;
        public Color newBadgeColor = new Color(0.9f, 0.25f, 0.2f, 1f);
        [Tooltip("裝備中的格子邊框與標籤顏色")]
        public Color equippedColor = new Color(1f, 0.85f, 0.6f);
        [Tooltip("差異預覽的數值名稱；沒列的用 PlayerStatId 的名字")]
        public StatLabel[] statLabels =
        {
            new StatLabel { stat = PlayerStatId.MaxHealth, label = "最大血量", format = "0" },
            new StatLabel { stat = PlayerStatId.MaxStamina, label = "最大耐力", format = "0" },
            new StatLabel { stat = PlayerStatId.StaminaRegen, label = "耐力回復", format = "0.#" },
            new StatLabel { stat = PlayerStatId.MoveSpeed, label = "跑速", format = "0.##" },
            new StatLabel { stat = PlayerStatId.RollStaminaCost, label = "翻滾耐力消耗", format = "0.#", lowerIsBetter = true },
            new StatLabel { stat = PlayerStatId.BlockDamageReduction, label = "格擋減傷", format = "0%" },
            new StatLabel { stat = PlayerStatId.ParryWindow, label = "盾反判定時間", format = "0.00" },
            new StatLabel { stat = PlayerStatId.ParryImbalance, label = "盾反失衡值", format = "0.#" },
            new StatLabel { stat = PlayerStatId.BoneCapacity, label = "骨頭持有上限", format = "0" },
            new StatLabel { stat = PlayerStatId.BoneHealAmount, label = "骨頭回血量", format = "0.#" },
            new StatLabel { stat = PlayerStatId.LightAttackDamage, label = "輕攻擊傷害（第一段）", format = "0.#" },
            new StatLabel { stat = PlayerStatId.HeavyAttackDamage, label = "重擊傷害", format = "0.#" },
            new StatLabel { stat = PlayerStatId.ExecutionDamage, label = "處決傷害", format = "0.#" },
        };

        [Header("傳送門")]
        [Tooltip("挑戰清單（目前只有 OIIA 貓一列；T24 改讀 BossDefinition）")]
        public string bossLabel = "OIIA 貓";
        [Tooltip("確認訊息：{0} = Boss 名稱")]
        public string departConfirmFormat = "要出發挑戰 {0} 嗎？";
        public string departYes = "出發";
        public string departNo = "取消";

        /// <summary>這項數值的顯示設定；沒列時 null。</summary>
        public StatLabel GetStat(PlayerStatId stat)
        {
            if (statLabels == null) return null;
            foreach (var s in statLabels)
            {
                if (s != null && s.stat == stat) return s;
            }
            return null;
        }

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
