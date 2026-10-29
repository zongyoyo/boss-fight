using System;
using BossFight.Hub;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.UI
{
    /// <summary>
    /// 休息區介面的文字與顏色（T20）：互動提示、站點面板、營火（T21）與技能樹（T23）、鐵匠鋪（T22）、傳送門的挑戰清單、村長的對話框與頭上的標記（T24）。
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
        [Tooltip("村長有任務可接時，提示改用這個動詞（「按 [E] 接任務」）")]
        public string chiefNewQuestVerb = "接任務";
        [Tooltip("村長有任務可回報時，提示改用這個動詞")]
        public string chiefTurnInVerb = "回報任務";
        public Color promptColor = Color.white;
        [Range(10, 80)] public int promptFontSize = 34;
        [Tooltip("提示離畫面下緣多高（Canvas 單位，參考解析度 1920×1080）")]
        public float promptY = 150f;

        [Header("站點面板")]
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

        [Header("技能樹")]
        [Tooltip("右上的剩餘點數：{0} = 技能點池")]
        public string skillPointsFormat = "剩餘技能點：{0}";
        [Tooltip("目前角色沒有技能樹：{0} = 角色名稱")]
        public string noSkillTreeFormat = "{0}沒有技能樹";
        [Tooltip("節點上的花費：{0} = 點數")]
        public string skillCostFormat = "{0} 點";
        [Tooltip("學過的節點上的字（取代花費）")]
        public string skillLearnedTag = "已學會";
        [Tooltip("說明框的狀態列")]
        public string skillLearnedText = "已學會";
        [Tooltip("可以學：{0} = 花費")]
        public string skillAvailableFormat = "花費 {0} 點，可以學習";
        [Tooltip("前置沒學：{0} = 前置節點的名稱（用 skillNameSeparator 隔開）")]
        public string skillMissingPrerequisiteFormat = "需要先學：{0}";
        public string skillNameSeparator = "、";
        [Tooltip("點數不夠：{0} = 花費，{1} = 剩餘點數")]
        public string skillNotEnoughPointsFormat = "技能點不足（需要 {0} 點，剩 {1} 點）";
        [Tooltip("差異預覽的開頭")]
        public string learnPreviewHeader = "學習後：";
        [Tooltip("沒選節點時說明框的字")]
        public string skillSelectHint = "選一個節點查看說明。";
        public string learnButtonLabel = "學習";
        public string resetSkillsLabel = "重置";
        [Tooltip("重置確認：{0} = 角色名稱，{1} = 退回的點數")]
        public string resetSkillsConfirmFormat = "要重置{0}的技能嗎？\n會退回 {1} 點技能點。";
        public string resetSkillsYes = "重置";
        public string resetSkillsNo = "取消";
        [Tooltip("Debug（開發者模式）按鈕：{0} = 一次給的點數")]
        public string debugGiveSkillPointsFormat = "給 {0} 技能點（Debug）";
        [Tooltip("節點的大小與格子間距（Canvas 單位；gridPosition 一格 = 間距）")]
        public Vector2 skillNodeSize = new Vector2(190f, 84f);
        public Vector2 skillNodeSpacing = new Vector2(220f, 140f);
        [Tooltip("前置連線的粗細")]
        [Min(1f)] public float skillLineWidth = 6f;
        [Tooltip("學過的節點的底色與字色")]
        public Color skillLearnedColor = new Color(0.86f, 0.64f, 0.28f, 1f);
        public Color skillLearnedTextColor = new Color(0.12f, 0.08f, 0.04f, 1f);
        [Tooltip("可以學的節點的外框")]
        public Color skillAvailableFrameColor = new Color(1f, 0.85f, 0.6f, 1f);
        [Tooltip("不能學的節點的不透明度")]
        [Range(0f, 1f)] public float skillLockedAlpha = 0.4f;
        [Tooltip("前置連線：還沒學、前置學過了")]
        public Color skillLineColor = new Color(1f, 1f, 1f, 0.25f);
        public Color skillLineLearnedColor = new Color(0.86f, 0.64f, 0.28f, 1f);

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
        [Tooltip("挑戰清單裡擊敗過的 Boss：{0} = BossDefinition.displayName")]
        public string defeatedBossFormat = "{0}（已擊敗）";
        [Tooltip("挑戰清單每列的間距（Canvas 單位）")]
        [Min(0f)] public float bossRowSpacing = 14f;
        [Tooltip("確認訊息：{0} = Boss 名稱")]
        public string departConfirmFormat = "要出發挑戰 {0} 嗎？";
        public string departYes = "出發";
        public string departNo = "取消";

        [Header("村長的對話框（T24）")]
        [Tooltip("逐字出現的速度（字/秒）")]
        [Min(1f)] public float dialogueCharsPerSecond = 30f;
        [Tooltip("一句顯示完時右下角的記號")]
        public string dialogueContinueMark = "▼";
        [Tooltip("記號上下跳動的幅度（Canvas 單位）與每秒次數")]
        [Min(0f)] public float dialogueContinueBob = 6f;
        [Min(0f)] public float dialogueContinueRate = 2f;
        public Color dialogueBoxColor = new Color(0.06f, 0.05f, 0.04f, 0.9f);
        public Color speakerColor = new Color(1f, 0.85f, 0.6f);
        [Tooltip("對話框離畫面下緣多高、大小（Canvas 單位）")]
        public float dialogueY = 60f;
        public Vector2 dialogueSize = new Vector2(1400f, 250f);
        [Tooltip("接任務的選項：任務名稱、獎勵；{0} = 任務名稱或獎勵")]
        public string questTitleFormat = "任務：{0}";
        public string questRewardsFormat = "獎勵：{0}";
        [Tooltip("獎勵之間的分隔、技能點的寫法（{0} = 點數）")]
        public string rewardSeparator = "、";
        public string rewardSkillPointsFormat = "技能點 ×{0}";
        public string acceptQuestLabel = "接受";
        public string declineQuestLabel = "再想想";

        [Header("村長頭上的標記（T24）")]
        [Tooltip("有可接的任務、有可回報的任務")]
        public string newQuestMarker = "!";
        public string turnInMarker = "?";
        public Color newQuestMarkerColor = new Color(1f, 0.85f, 0.2f);
        public Color turnInMarkerColor = new Color(0.55f, 0.9f, 1f);
        [Range(20, 200)] public int markerFontSize = 96;
        [Tooltip("上下浮動的幅度（Canvas 單位）與每秒次數")]
        [Min(0f)] public float markerBob = 10f;
        [Min(0f)] public float markerBobRate = 1.2f;

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
