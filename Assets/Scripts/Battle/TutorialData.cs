using UnityEngine;

namespace BossFight.Battle
{
    [CreateAssetMenu(menuName = "BossFight/Tutorial/Sequence", fileName = "Tutorial")]
    public class TutorialData : ScriptableObject
    {
        [Header("開場觸發")]
        [Min(0f), Tooltip("玩家進入此水平距離 (m) 時，小花冒出並招呼玩家")]
        public float greetingRadius = 10f;
        [Tooltip("小花冒出後的招呼台詞")][TextArea]
        public string greeting = "欸那邊的胖呆，過來過來";
        [Min(0f), Tooltip("招呼文字出現後，經過幾秒自動收起；不影響靠近互動")]
        public float greetingDuration = 2f;
        [Min(0f), Tooltip("玩家進入此水平距離 (m) 後按互動鍵，才開始正式劇情")]
        public float startRadius = 1.5f;
        [Tooltip("接近後顯示的互動提示，{0} 為目前裝置的互動鍵")]
        public string interactionHint = "按 [{0}] 與神祕小花對話";
        [Header("劇本")]
        [Tooltip("說話者名稱")] public string speaker;
        [Tooltip("完整開場與召喚時點")] public TutorialLine[] opening;
        [Tooltip("依序執行的步驟")] public TutorialStepData[] steps;
        [Tooltip("完整結尾")] public TutorialLine[] ending;
        [Tooltip("盾反額外稱讚")][TextArea] public string parryPraise;
        [Tooltip("打菊花的彩蛋台詞")][TextArea] public string prankLine;
        [Header("補給與節奏")]
        [Min(0), Tooltip("教學開始持有量")] public int initialBones;
        [Min(0), Tooltip("指定台詞後給的骨頭")] public int grantedBones;
        [Min(0f), Tooltip("打勾後等多久才稱讚")] public float completionDelay;
        [Min(0f), Tooltip("結尾揭穿陷阱後的停頓")] public float endingPause;
    }
}
