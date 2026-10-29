using System;
using BossFight.Boss;
using BossFight.Cutscene;
using UnityEngine;

namespace BossFight.Battle
{
    public enum TutorialCondition { Move, Heal, Dodge, Block, Hit, Execute }
    // 新的動作加在最後，保留既有資產的編號
    public enum TutorialCue { None, Summon, Whip, GiveBone, Slap, Demo, ShowBars, Rumble }
    [Serializable]
    public class TutorialLine
    {
        [Tooltip("菊花逐句台詞；空白 = 不講話，只執行動作")][TextArea] public string text;
        [Tooltip("說這句時菊花的動作")] public DaisyGesture gesture;
        [Tooltip("說這句時小樹精的動作")] public SaplingGesture saplingGesture;
        [Tooltip("這句的鏡頭；None = 雙人鏡頭")] public TutorialShot shot;
        [Tooltip("確認這句之後執行；動作完成才播下一句")] public TutorialCue after;
        [Tooltip("動作期間的鏡頭；None = 沿用這句的鏡頭")] public TutorialShot cueShot;
    }
    [CreateAssetMenu(menuName = "BossFight/Tutorial/Step", fileName = "TutorialStep")]
    public class TutorialStepData : ScriptableObject
    {
        [Tooltip("步驟識別碼")] public string id;
        [Tooltip("提示上方的步驟名稱（「2 / 6　啃骨頭」）")] public string title;
        [Tooltip("先播完說明與動作才開放操作")] public TutorialLine[] opening;
        [Tooltip("{0} 為目前裝置的按鍵")] public string hintFormat;
        [Tooltip("Project-wide Action 路徑")] public string actionPath;
        [Tooltip("事件完成條件")] public TutorialCondition condition;
        [Min(0f), Tooltip("距離或成功次數")] public float target;
        [Tooltip("操作期間的小樹精模式")] public SaplingMode mode;
        [Tooltip("提前攻擊彈開")] public bool invulnerable = true;
        [Tooltip("完成稱讚")][TextArea] public string praise;
        [Tooltip("說稱讚時菊花的動作")] public DaisyGesture praiseGesture;
    }
}
