using System;
using BossFight.Boss;
using UnityEngine;

namespace BossFight.Battle
{
    public enum TutorialCondition { Move, Heal, Dodge, Block, Hit, Execute }
    public enum TutorialCue { None, Summon, Whip, GiveBone, Slap }
    [Serializable]
    public class TutorialLine
    {
        [Tooltip("菊花逐句台詞")][TextArea] public string text;
        [Tooltip("確認這句之後執行；動作完成才播下一句")] public TutorialCue after;
    }
    [CreateAssetMenu(menuName = "BossFight/Tutorial/Step", fileName = "TutorialStep")]
    public class TutorialStepData : ScriptableObject
    {
        [Tooltip("步驟識別碼")] public string id;
        [Tooltip("先播完說明與動作才開放操作")] public TutorialLine[] opening;
        [Tooltip("{0} 為目前裝置的按鍵")] public string hintFormat;
        [Tooltip("Project-wide Action 路徑")] public string actionPath;
        [Tooltip("事件完成條件")] public TutorialCondition condition;
        [Min(0f), Tooltip("距離或成功次數")] public float target;
        [Tooltip("操作期間的小樹精模式")] public SaplingMode mode;
        [Tooltip("提前攻擊彈開")] public bool invulnerable = true;
        [Tooltip("完成稱讚")][TextArea] public string praise;
    }
}
