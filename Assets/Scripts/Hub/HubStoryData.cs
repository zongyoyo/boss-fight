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
        [Tooltip("村長走過來後講的話，一行一句；講完開放第一個任務")]
        [TextArea(1, 3)] public List<string> introLines = new List<string>
        {
            "喔喔，你就是新來的冒險者吧？來得正好。",
            "最近森林深處出現了一隻怪貓，整天一邊唱歌一邊原地轉圈。",
            "牠轉得越快，森林裡的雷雨就越大，連樹都快被吹倒了。",
            "村子裡沒人敢靠近牠……能請你幫幫忙嗎？",
            "準備好了就到小屋前找我，我再跟你說詳細的。",
        };
        [Tooltip("淡入後等幾秒村長才開始走過來")]
        [Min(0f)] public float introDelay = 0.6f;
        [Tooltip("村長走路的速度 (m/s)")]
        [Min(0.1f)] public float walkSpeed = 3.5f;
        [Tooltip("村長轉身的速度（度/秒）")]
        [Min(1f)] public float turnSpeed = 540f;

        [Header("對話")]
        [Tooltip("沒有任務可講時（全部完成或還沒開放）的閒聊，一行一句")]
        [TextArea(1, 3)] public List<string> idleLines = new List<string>
        {
            "多虧了你，森林最近安靜多了。",
        };
        [Tooltip("接下任務後村長補的一句：{0} = 任務名稱")]
        public string questAcceptedFormat = "「{0}」就拜託你了，路上小心！";
        [Tooltip("回報任務時最後一句拿到的獎勵：{0} = 獎勵（rewardSeparator 隔開）")]
        public string rewardLineFormat = "（獲得：{0}）";
        public string rewardSeparator = "、";
        [Tooltip("技能點獎勵的寫法：{0} = 點數")]
        public string skillPointsFormat = "技能點 ×{0}";
    }
}
