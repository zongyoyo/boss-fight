using System.Collections.Generic;
using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>
    /// 休息區的劇情（T24，企劃書「村長與任務」）：村長的名稱、第一次進休息區的開場對白、沒有任務可講時的閒聊、接任務與拿獎勵時村長補的一句，
    /// 以及開場時村長走過來的節奏。任務自己的對白在 QuestData。對白都是佔位文字（正式文本列在企劃書的待決議事項）。
    /// VillageChief 讀；資產在 Assets/ScriptableObjects/Hub/HubStory.asset（HubBuilder 沒有時建立）。
    /// </summary>
    [CreateAssetMenu(fileName = "HubStory", menuName = "BossFight/Hub/Hub Story Data")]
    public class HubStoryData : ScriptableObject
    {
        [Header("村長")]
        [Tooltip("對話框上的說話者名稱")]
        public string chiefName = "村長";

        [Header("開場劇情（第一次進休息區）")]
        [Tooltip("沒有序章結果時（開發者略過序章）村長走過來後講的話，一行一句；講完直接接第一個任務的說明與「接受／再想想」")]
        [TextArea(1, 3)] public List<string> introLines = new List<string>
        {
            "喔喔，你就是新來的冒險者吧？來得正好。",
            "村子最近被一隻怪貓搞得雞犬不寧，我跟你說說那隻貓的事吧。",
        };
        [Tooltip("淡入後等幾秒村長才開始走過來")]
        [Min(0f)] public float introDelay = 0.6f;
        [Header("序章交接")]
        [Tooltip("被擊倒後的首次對白；最後一句引出任務，講完直接接任務說明")][TextArea(1, 3)] public List<string> knockdownLines = new List<string>
        {
            "喔，你醒啦。昨天傍晚在森林邊撿到你，整個人倒在樹根堆裡。",
            "看你這一身傷……該不會是去惹了大樹守衛吧？以你現在的本事，還打不過那傢伙的。",
            "先在附近累積點實力吧。最近森林裡冒出一隻會轉圈的怪貓，正好拿牠來練練手。",
            "我跟你說說那隻貓的事吧。",
        };
        [Tooltip("打贏之後的首次對白；最後一句引出任務，講完直接接任務說明")][TextArea(1, 3)] public List<string> victoryLines = new List<string>
        {
            "喔，你回來啦。剛才森林深處那陣地鳴……該不會是你搞出來的吧？",
            "連大樹守衛都攔不住你，真了不起。",
            "不過最近森林裡又冒出一隻會轉圈的怪貓，還得麻煩你走一趟。",
            "我跟你說說那隻貓的事吧。",
        };
        [Tooltip("村長走路的速度 (m/s)")]
        [Min(0.1f)] public float walkSpeed = 3.5f;
        [Tooltip("村長轉身的速度（度/秒）")]
        [Min(1f)] public float turnSpeed = 540f;
        [Tooltip("村長在原位、有任務可接或可回報時，玩家走進這個距離 (m) 就轉身面向玩家")]
        [Min(0f)] public float greetRadius = 6f;

        [Header("對話")]
        [Tooltip("沒有任務可講時（全部完成或還沒開放）的閒聊，一行一句")]
        [TextArea(1, 3)] public List<string> idleLines = new List<string>
        {
            "多虧了你，森林最近安靜多了。",
        };
        [Tooltip("接下任務後村長補的一句：{0} = 任務名稱；之後接任務自己的 acceptedLines")]
        public string questAcceptedFormat = "「{0}」就拜託你了。";
        [Tooltip("任務說明後選「再想想」（或按返回）時村長講的話，一行一句；講完頭上亮「!」")]
        [TextArea(1, 3)] public List<string> declineLines = new List<string>
        {
            "好吧，等你準備好了，再來小屋前找我。",
        };
        [Tooltip("回報任務時最後一句拿到的獎勵：{0} = 獎勵（rewardSeparator 隔開）")]
        public string rewardLineFormat = "（獲得：{0}）";
        public string rewardSeparator = "、";
        [Tooltip("技能點獎勵的寫法：{0} = 點數")]
        public string skillPointsFormat = "技能點 ×{0}";
    }
}
