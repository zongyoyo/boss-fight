using UnityEngine;

namespace BossFight.UI
{
    /// <summary>戰鬥 UI 的數值與文字：Boss 血條刻度、血條動畫、出現與淡出的時間、顏色、勝敗畫面的文字。BattleUI 底下的元件都讀這一份。</summary>
    [CreateAssetMenu(menuName = "BossFight/Battle UI Data", fileName = "BattleUIData")]
    public class BattleUIData : ScriptableObject
    {
        [Header("Boss 血條")]
        [Tooltip("Boss 血條上的刻度（血量比例 0..1）：企劃書標出 50%（第二階段）與 20%（狂暴）")]
        public float[] bossHealthTicks = { 0.5f, 0.2f };
        [Tooltip("沒有開場字卡時顯示的 Boss 名稱；有字卡時用字卡上的名稱")]
        public string fallbackBossName = "OIIA 貓";

        [Header("血條動畫（秒，真實時間）")]
        [Tooltip("扣血後淺色的「延遲扣血」停住多久才開始縮")]
        [Min(0f)] public float trailDelay = 0.5f;
        [Tooltip("延遲扣血每秒縮掉的比例（1 = 整條）")]
        [Min(0.01f)] public float trailSpeed = 0.8f;
        [Tooltip("數值改變時主條追上去的速度（每秒比例）；越大越即時")]
        [Min(0.01f)] public float fillSpeed = 6f;
        [Tooltip("耐力歸零時耐力條閃爍的秒數")]
        [Min(0f)] public float emptyFlashTime = 0.6f;
        [Tooltip("閃爍每秒幾下（耐力歸零）")]
        [Min(0.1f)] public float flashRate = 6f;

        [Header("出現與淡出（秒，真實時間）")]
        [Tooltip("HUD 淡入淡出的時間")]
        [Min(0.01f)] public float hudFadeTime = 0.35f;
        [Tooltip("Boss 血條從下方滑入的時間")]
        [Min(0.01f)] public float bossBarSlideTime = 0.6f;
        [Tooltip("Boss 血條滑入的距離（Canvas 單位，參考解析度 1920×1080）")]
        [Min(0f)] public float bossBarSlideDistance = 160f;
        [Tooltip("勝敗畫面淡入的時間")]
        [Min(0.01f)] public float resultFadeTime = 0.8f;

        [Header("鎖定標記")]
        [Tooltip("鎖定時從幾倍大縮回 1")]
        [Min(1f)] public float lockMarkerPopScale = 1.8f;
        [Tooltip("縮回 1 的時間（秒）")]
        [Min(0.01f)] public float lockMarkerPopTime = 0.15f;
        [Tooltip("呼吸縮放的幅度（0.1 = ±10%）")]
        [Range(0f, 0.5f)] public float lockMarkerPulse = 0.1f;
        [Tooltip("呼吸每秒幾下")]
        [Min(0f)] public float lockMarkerPulseRate = 1.5f;

        [Header("處決標記（Boss 頭暈中）")]
        [Tooltip("紅色圓環與中心點的顏色")]
        public Color executionMarkerColor = new Color(1f, 0.15f, 0.12f, 0.95f);
        [Tooltip("出現時從幾倍大縮回 1")]
        [Min(1f)] public float executionMarkerPopScale = 2.2f;
        [Tooltip("縮回 1 的時間（秒）")]
        [Min(0.01f)] public float executionMarkerPopTime = 0.2f;
        [Tooltip("脈動縮放的幅度（0.15 = ±15%）")]
        [Range(0f, 0.5f)] public float executionMarkerPulse = 0.15f;
        [Tooltip("脈動每秒幾下")]
        [Min(0f)] public float executionMarkerPulseRate = 2.5f;

        [Header("顏色")]
        public Color playerHealthColor = new Color(0.86f, 0.2f, 0.2f);
        public Color staminaColor = new Color(0.35f, 0.85f, 0.4f);
        [Tooltip("耐力歸零閃爍的顏色")]
        public Color staminaEmptyColor = new Color(1f, 0.85f, 0.3f);
        [Tooltip("舉盾加速回復中的耐力條")]
        public Color staminaBoostColor = new Color(0.55f, 1f, 0.95f);
        public Color bossHealthColor = new Color(0.72f, 0.18f, 0.85f);
        [Tooltip("延遲扣血（淺色）")]
        public Color trailColor = new Color(1f, 0.9f, 0.65f);
        public Color staggerColor = new Color(1f, 0.78f, 0.2f);
        [Tooltip("頭暈中失衡條（倒數）的顏色")]
        public Color stunnedColor = new Color(1f, 1f, 0.75f);
        [Tooltip("剩下的骨頭")]
        public Color boneColor = new Color(0.98f, 0.93f, 0.82f);
        [Tooltip("用掉的骨頭")]
        public Color boneUsedColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        [Tooltip("骨頭下方的累積進度條（下一根骨頭）")]
        public Color boneProgressColor = new Color(0.98f, 0.8f, 0.45f);

        [Header("文字")]
        [Tooltip("頭暈中顯示在失衡條上；{0} = 處決鍵（Player/Execute 目前裝置的按鍵）")]
        public string stunnedLabel = "頭暈中！靠近按 {0} 處決";
        public string victoryTitle = "勝利";
        [Tooltip("{0} = Boss 名稱")]
        public string victorySubtitle = "{0} 討伐成功";
        public string defeatTitle = "落敗";
        public string defeatSubtitle = "刀盾狗倒下了";
        public string victoryButton = "再挑戰";
        public string defeatButton = "重試";
        [Tooltip("按鈕下面的提示（用鍵鼠時）")]
        public string buttonHintKeyboard = "Enter 確認";
        [Tooltip("按鈕下面的提示（用手把時）")]
        public string buttonHintGamepad = "A 確認";
        public Color victoryColor = new Color(1f, 0.85f, 0.4f);
        public Color defeatColor = new Color(0.85f, 0.25f, 0.25f);
    }
}
