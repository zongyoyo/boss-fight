using UnityEngine;

namespace BossFight.Cutscene
{
    /// <summary>序章演出節奏與佔位視聽參數；遊戲中只讀。</summary>
    [CreateAssetMenu(menuName = "BossFight/Cutscene/Prologue", fileName = "PrologueCutscene")]
    public class PrologueCutsceneData : ScriptableObject
    {
        [Header("時間軸（秒）")]
        [Tooltip("登場七鏡的長度，依分鏡表順序")]
        public float[] entranceShots = { 1f, 1.5f, 1.5f, 1.5f, 1.5f, 2f, 0.5f };
        [Tooltip("序章開場（玩家起身、鏡頭帶過空地與北側樹根、土堆動兩下）的秒數")][Min(0f)] public float openingDuration = 3.5f;
        [Tooltip("轉階段演出秒數")][Min(0f)] public float transitionDuration = 5f;
        [Tooltip("擊倒演出秒數")][Min(0f)] public float knockdownDuration = 5f;
        [Tooltip("勝利演出秒數")][Min(0f)] public float victoryDuration = 10f;
        [Tooltip("休息區醒來秒數")][Min(0f)] public float wakeDuration = 4f;
        [Tooltip("擊倒慢動作的真實秒數")][Min(0f)] public float knockdownSlowTime = 0.4f;
        [Tooltip("勝利慢動作的真實秒數")][Min(0f)] public float victorySlowTime = 0.6f;
        [Tooltip("結局慢動作倍率")][Range(0.01f, 1f)] public float slowScale = 0.3f;
        [Tooltip("擊倒收黑後維持秒數")][Min(0f)] public float blackHold = 1.5f;
        [Tooltip("轉階段對白最多等待秒數")][Min(0f)] public float dialogueTimeout = 3f;
        [Header("演出站位")]
        [Tooltip("轉階段衝擊波把玩家推到守衛南側的距離 (m)；衝擊波環也放大到這個半徑")][Min(0f)] public float pushRadius = 9f;
        [Header("純視覺初版")]
        [Tooltip("守衛藏在地下的模型深度")][Min(0f)] public float buriedDepth = 11f;
        [Tooltip("擊倒鏡頭傾斜度")][Range(-30f, 30f)] public float cameraRoll = 10f;
        [Tooltip("暗紅天色") ] public Color darkLightColor = new Color(0.8f, 0.08f, 0.045f);
        [Tooltip("暗紅天色的主光強度")][Min(0f)] public float darkLightIntensity = 0.25f;
        [Tooltip("暗紅天色的飽和度（Color Adjustments）")][Range(-100f, 100f)] public float darkSaturation = -45f;
        [Tooltip("暗紅天色的濾色（Color Adjustments）")] public Color darkColorFilter = new Color(1f, 0.45f, 0.38f);
        [Tooltip("戰鬥霧色") ] public Color darkFog = new Color(0.16f, 0.035f, 0.025f);
        [Tooltip("濃霧密度")][Min(0f)] public float darkFogDensity = 0.035f;
        [Tooltip("擊倒低通頻率")][Min(10f)] public float lowPassFrequency = 550f;
        [Tooltip("模糊焦距")][Min(0.1f)] public float blurFocus = 0.2f;
        [Tooltip("光圈")][Range(1f, 32f)] public float blurAperture = 1f;
        [Tooltip("模糊的焦長 (mm)")][Range(1f, 300f)] public float blurFocalLength = 70f;
        [Tooltip("模糊開始前的對焦距離 (m)")][Min(0.1f)] public float blurStartFocus = 10f;
        [Tooltip("樹冠完全枯萎時剩下的比例（x、y、z）")] public Vector3 witherScale = new Vector3(0.4f, 0.2f, 0.4f);
        [Tooltip("樹冠完全枯萎時往前垂的角度（度）")][Range(0f, 90f)] public float witherTilt = 35f;
        [Tooltip("轉階段衝擊波環的厚度 (m)")][Min(0f)] public float waveHeight = 0.12f;
        [Tooltip("視野暗角上限")][Range(0f, 1f)] public float vignetteIntensity = 0.85f;
        [Tooltip("登場後持續的戰鬥暗角比例")][Range(0f, 1f)] public float battleVignette = 0.4f;
        [Header("台詞與字卡")]
        [Tooltip("登場字卡") ] public string title = "大樹守衛";
        [Tooltip("守衛告別台詞（說話者是 GuardianBattleData.guardianName）")][TextArea] public string farewellLine = "……去吧，冒險者。";
        [Tooltip("小花勝利台詞（說話者是 TutorialData.speaker）")][TextArea] public string surpriseLine = "欸……你真的贏了？";
        [Header("專用佔位音樂")]
        [Tooltip("教學音樂") ] public AudioClip tutorialMusic;
        [Tooltip("守衛一階段音樂") ] public AudioClip phase1Music;
        [Tooltip("守衛二階段音樂") ] public AudioClip phase2Music;
    }
}
