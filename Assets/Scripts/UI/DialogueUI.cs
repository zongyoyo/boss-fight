using System.Collections.Generic;
using System.Text;
using BossFight.Hub;
using BossFight.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 村長的對話框（T24，企劃書「村長與任務」）：畫面下方，說話者名稱加逐字出現的文字。只訂閱 VillageChief.OnDialogue（開始一段對話）與 HubManager 的站點關閉。
    /// 前進：滑鼠點擊（UI/Click）、Enter／手把 A（UI/Submit）、互動鍵（Player/Interact）——字還在跑時先顯示全文，顯示完再按下一句；
    /// 開始那一幀的按鍵不算（打開村長用的 E 不會直接跳過第一句）。最後一句講完：要問接不接任務時顯示任務名稱、說明、獎勵與「接受／再想想」，
    /// 接受呼叫 VillageChief.Accept（村長再講一句），再想想或沒有選項時 HubManager.CloseStation。
    /// 返回讀 UI/Cancel（Esc、手把 B）：直接關掉（選項那一步等於再想想）。文字、顏色、速度在 HubUIData 的「村長的對話框」。由 HubUIBuilder 建立與接線。
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueUI : MonoBehaviour
    {
        [SerializeField] HubUIData data;
        [SerializeField] HubManager hub;
        [SerializeField] VillageChief chief;
        [SerializeField] EventSystem eventSystem;
        [Tooltip("整個對話框")]
        [SerializeField] CanvasGroup group;
        [SerializeField] Text speakerText;
        [SerializeField] Text bodyText;
        [Tooltip("一句顯示完時右下角的記號")]
        [SerializeField] Text continueMark;

        [Header("接任務")]
        [Tooltip("選項那一步才出現：任務資訊與兩個按鈕")]
        [SerializeField] GameObject choiceGroup;
        [SerializeField] Text questText;
        [SerializeField] Button acceptButton;
        [SerializeField] Button declineButton;

        readonly List<string> lines = new List<string>();
        readonly StringBuilder builder = new StringBuilder();
        InputAction submitAction, clickAction, interactAction, cancelAction;
        QuestData offer;
        bool visible, choosing, lineDone;
        int lineIndex, startedFrame;
        float shownChars;
        Vector2 continueBase;

        void Awake()
        {
            var actions = InputSystem.actions;
            if (actions != null)
            {
                submitAction = actions.FindAction("UI/Submit", false);
                clickAction = actions.FindAction("UI/Click", false);
                interactAction = actions.FindAction("Player/Interact", false);
                cancelAction = actions.FindAction("UI/Cancel", false);
            }
            acceptButton.onClick.AddListener(Accept);
            declineButton.onClick.AddListener(Close);
            if (data != null)
            {
                acceptButton.GetComponentInChildren<Text>(true).text = data.acceptQuestLabel;
                declineButton.GetComponentInChildren<Text>(true).text = data.declineQuestLabel;
                continueMark.text = data.dialogueContinueMark;
            }
            continueBase = continueMark.rectTransform.anchoredPosition;
            SetVisible(false);
        }

        void OnEnable()
        {
            submitAction?.Enable();
            clickAction?.Enable();
            interactAction?.Enable();
            cancelAction?.Enable();
            if (chief != null) chief.OnDialogue += Show;
            if (hub != null) hub.OnStationClosed += HandleStationClosed;
        }

        void OnDisable()
        {
            if (chief != null) chief.OnDialogue -= Show;
            if (hub != null) hub.OnStationClosed -= HandleStationClosed;
        }

        void Show(VillageChief.DialogueView view)
        {
            lines.Clear();
            lines.AddRange(view.lines);
            offer = view.offer;
            speakerText.text = view.speaker;
            startedFrame = Time.frameCount;
            choosing = false;
            choiceGroup.SetActive(false);
            SetVisible(true);
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            lineIndex = -1;
            NextLine();
        }

        void HandleStationClosed(Interactable target)
        {
            if (target != chief || !visible) return;
            SetVisible(false);
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
        }

        void Update()
        {
            if (!visible) return;
            if (cancelAction != null && cancelAction.WasPressedThisFrame())
            {
                Close();
                return;
            }
            if (choosing)
            {
                // 滑鼠點到空白處會取消選取：選回「接受」，鍵盤、手把才導覽得下去
                if (eventSystem != null && eventSystem.currentSelectedGameObject == null) eventSystem.SetSelectedGameObject(acceptButton.gameObject);
                return;
            }

            if (!lineDone && data != null)
            {
                shownChars += data.dialogueCharsPerSecond * Time.deltaTime;
                ShowChars();
            }
            continueMark.enabled = lineDone;
            if (lineDone && data != null)
            {
                float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * data.dialogueContinueRate * Mathf.PI)) * data.dialogueContinueBob;
                continueMark.rectTransform.anchoredPosition = continueBase + new Vector2(0f, bob);
            }

            if (Time.frameCount == startedFrame || !AdvancePressed()) return;
            if (!lineDone)
            {
                shownChars = lines[lineIndex].Length; // 先顯示全文
                ShowChars();
            }
            else NextLine();
        }

        bool AdvancePressed() =>
            (submitAction != null && submitAction.WasPressedThisFrame()) ||
            (clickAction != null && clickAction.WasPressedThisFrame()) ||
            (interactAction != null && interactAction.WasPressedThisFrame());

        void NextLine()
        {
            lineIndex++;
            if (lineIndex < lines.Count)
            {
                shownChars = 0f;
                lineDone = false;
                ShowChars();
                return;
            }
            if (offer != null) ShowChoice();
            else Close();
        }

        void ShowChars()
        {
            string line = lines[lineIndex];
            int count = Mathf.Min(line.Length, Mathf.FloorToInt(shownChars));
            bodyText.text = count >= line.Length ? line : line.Substring(0, count);
            lineDone = count >= line.Length;
            continueMark.enabled = lineDone;
        }

        // 最後一句講完：任務名稱、說明、獎勵，與「接受／再想想」
        void ShowChoice()
        {
            choosing = true;
            continueMark.enabled = false;
            builder.Clear();
            if (data != null) builder.Append(string.Format(data.questTitleFormat, offer.title));
            if (!string.IsNullOrEmpty(offer.description)) builder.Append('\n').Append(offer.description);
            string rewards = DescribeRewards(offer.rewards);
            if (rewards.Length > 0 && data != null) builder.Append('\n').Append(string.Format(data.questRewardsFormat, rewards));
            questText.text = builder.ToString();
            bodyText.text = string.Empty;
            choiceGroup.SetActive(true);
            if (eventSystem != null) eventSystem.SetSelectedGameObject(acceptButton.gameObject);
        }

        string DescribeRewards(List<RewardData> rewards)
        {
            if (rewards == null || data == null) return string.Empty;
            var text = new StringBuilder();
            foreach (var reward in rewards)
            {
                string part = reward != null ? reward.Describe(data.rewardSkillPointsFormat) : string.Empty;
                if (string.IsNullOrEmpty(part)) continue;
                if (text.Length > 0) text.Append(data.rewardSeparator);
                text.Append(part);
            }
            return text.ToString();
        }

        void Accept()
        {
            if (!choosing || chief == null || offer == null) return;
            if (!chief.Accept(offer)) Close(); // 接了的話村長會再送一段對話（Show）
        }

        void Close()
        {
            if (hub != null) hub.CloseStation();
        }

        void SetVisible(bool visible)
        {
            this.visible = visible;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
