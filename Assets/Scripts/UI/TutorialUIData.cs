using BossFight.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    [CreateAssetMenu(menuName = "BossFight/Tutorial/UI", fileName = "TutorialUIData")]
    public class TutorialUIData : ScriptableObject
    {
        [Tooltip("操作提示距離畫面上緣")] public float top = 95f;
        [Tooltip("提示尺寸")] public Vector2 size = new Vector2(1200f, 80f);
        [Tooltip("提示文字大小")] public int fontSize = 34;
        [Tooltip("完成記號")] public string checkMark = "✓";
        [Tooltip("次數／距離顯示格式")] public string progressFormat = "  {0:0.#} / {1:0.#}";
        [Tooltip("方向鍵之間的分隔")] public string bindingSeparator = " / ";
        [Tooltip("提示上方的步驟進度：{0} = 第幾步、{1} = 總步數、{2} = 步驟名稱")] public string stepCounterFormat = "{0} / {1}　{2}";
        [Tooltip("步驟進度的文字大小")] public int counterFontSize = 24;
        [Tooltip("步驟進度的顏色")] public Color counterColor = new Color(1f, 0.85f, 0.6f);
        [Tooltip("提示文字顏色")] public Color textColor = Color.white;
        [Tooltip("完成顏色")] public Color completeColor = new Color(0.55f, 1f, 0.65f);
        [Min(0f), Tooltip("完成淡出秒數")] public float fadeTime = 0.45f;
        [Header("對話框")]
        [Tooltip("對話框水平與垂直內距")] public Vector2 dialoguePadding = new Vector2(50f, 20f);
        [Min(0f), Tooltip("說話者標題列高度")] public float speakerHeight = 50f;
        [Min(0f), Tooltip("黃字標題與白字內文的間距")] public float speakerBodyGap = 24f;
    }
}
