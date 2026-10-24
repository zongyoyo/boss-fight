using UnityEngine;

namespace BossFight.Battle
{
    /// <summary>戰鬥流程的數值：勝敗結算的節奏（慢動作、結算畫面出現前的等待、勝利時音樂淡出）與存檔用的 Boss id。BattleManager 讀。</summary>
    [CreateAssetMenu(menuName = "BossFight/Battle Settings", fileName = "BattleSettings")]
    public class BattleSettings : ScriptableObject
    {
        [Header("存檔")]
        [Tooltip("這場 Boss 的 id：勝利時記進存檔的擊敗紀錄（T24 起改用 BossDefinition 的 id）")]
        public string bossId = "oiia_cat";

        [Header("勝利")]
        [Tooltip("Boss 倒下後幾秒（真實時間）出現勝利畫面；這段期間玩家還能動，看 Boss 倒地")]
        [Min(0f)] public float victoryResultDelay = 2.5f;
        [Tooltip("最後一擊的慢動作倍率（Time.timeScale）；1 = 不放慢")]
        [Range(0.05f, 1f)] public float victorySlowMotionScale = 0.3f;
        [Tooltip("慢動作持續秒數（真實時間）")]
        [Min(0f)] public float victorySlowMotionTime = 0.8f;

        [Header("失敗")]
        [Tooltip("玩家倒下後幾秒（真實時間）出現失敗畫面")]
        [Min(0f)] public float defeatResultDelay = 2f;

        [Header("音樂")]
        [Tooltip("勝利時戰鬥音樂淡出的秒數（真實時間）；失敗時音樂照播（嘲諷）")]
        [Min(0f)] public float resultMusicFadeTime = 2f;
    }
}
