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
        [Tooltip("轉階段演出秒數")][Min(0f)] public float transitionDuration = 5f;
        [Tooltip("擊倒演出秒數")][Min(0f)] public float knockdownDuration = 5f;
        [Tooltip("勝利演出秒數")][Min(0f)] public float victoryDuration = 10f;
        [Tooltip("休息區醒來秒數")][Min(0f)] public float wakeDuration = 4f;
        [Tooltip("擊倒慢動作的真實秒數")][Min(0f)] public float knockdownSlowTime = 0.4f;
        [Tooltip("勝利慢動作的真實秒數")][Min(0f)] public float victorySlowTime = 0.6f;
        [Tooltip("結局慢動作倍率")][Range(0.01f, 1f)] public float slowScale = 0.3f;
        [Tooltip("擊倒收黑後維持秒數")][Min(0f)] public float blackHold = 1.5f;
        [Tooltip("轉階段對白最多等待秒數")][Min(0f)] public float dialogueTimeout = 3f;
        [Tooltip("Boss 血條滑入秒數")][Min(0f)] public float hudSlideTime = 0.5f;
        [Header("演出站位")]
        [Tooltip("守衛南側推開終點，距守衛公尺；與 GuardianBattleData 一致")][Min(0f)] public float pushRadius = 9f;
        [Header("純視覺初版")]
        [Tooltip("守衛藏在地下的模型深度")][Min(0f)] public float buriedDepth = 11f;
        [Tooltip("擊倒鏡頭傾斜度")][Range(-30f, 30f)] public float cameraRoll = 10f;
        [Tooltip("暗紅天色") ] public Color darkLightColor = new Color(0.8f, 0.08f, 0.045f);
        [Tooltip("暗紅天色的主光強度")][Min(0f)] public float darkLightIntensity = 0.25f;
        [Tooltip("戰鬥霧色") ] public Color darkFog = new Color(0.16f, 0.035f, 0.025f);
        [Tooltip("濃霧密度")][Min(0f)] public float darkFogDensity = 0.035f;
        [Tooltip("擊倒低通頻率")][Min(10f)] public float lowPassFrequency = 550f;
        [Tooltip("模糊焦距")][Min(0.1f)] public float blurFocus = 0.2f;
        [Tooltip("光圈")][Range(1f, 32f)] public float blurAperture = 1f;
        [Tooltip("視野暗角上限")][Range(0f, 1f)] public float vignetteIntensity = 0.85f;
        [Tooltip("登場後持續的戰鬥暗角比例")][Range(0f, 1f)] public float battleVignette = 0.4f;
        [Header("台詞與字卡")]
        [Tooltip("登場字卡") ] public string title = "大樹守衛";
        [Tooltip("守衛名稱") ] public string guardianSpeaker = "大樹守衛";
        [Tooltip("全力台詞")][TextArea] public string transitionLine = "很少有人能堅持到這裡，我認可你了，接下來我要出全力了。";
        [Tooltip("守衛告別台詞")][TextArea] public string farewellLine = "……去吧，冒險者。";
        [Tooltip("小花名稱") ] public string mentorSpeaker = "神祕小花";
        [Tooltip("小花勝利台詞")][TextArea] public string surpriseLine = "欸……你真的贏了？";
        [Header("專用佔位音樂")]
        [Tooltip("教學音樂") ] public AudioClip tutorialMusic;
        [Tooltip("守衛一階段音樂") ] public AudioClip phase1Music;
        [Tooltip("守衛二階段音樂") ] public AudioClip phase2Music;
    }
}
