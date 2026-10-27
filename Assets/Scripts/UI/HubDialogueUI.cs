using System.Collections.Generic;
using System.Text;
using BossFight.Core;
using BossFight.Hub;
using BossFight.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>村長專用的任務選項轉接；共用對話框不認識 Hub。</summary>
    public class HubDialogueUI : MonoBehaviour
    {
        [SerializeField] HubUIData data;
        [SerializeField] HubManager hub;
        [SerializeField] VillageChief chief;
        [SerializeField] DialogueSession session;
        [SerializeField] EventSystem eventSystem;
        [SerializeField] CanvasGroup choiceCanvas;
        [SerializeField] GameObject choiceGroup;
        [SerializeField] Text questText;
        [SerializeField] Button acceptButton;
        [SerializeField] Button declineButton;
        QuestData offer;
        bool active, choosing;
        InputAction cancelAction;
        void Awake()
        {
            cancelAction = InputSystem.actions?.FindAction("UI/Cancel", false);
            acceptButton.onClick.AddListener(Accept);
            declineButton.onClick.AddListener(Close);
            acceptButton.GetComponentInChildren<Text>(true).text = data.acceptQuestLabel;
            declineButton.GetComponentInChildren<Text>(true).text = data.declineQuestLabel;
            SetChoice(false);
        }
        void OnEnable()
        {
            chief.OnDialogue += Show;
            hub.OnStationClosed += HandleClosed;
            session.OnFinished += HandleFinished;
            cancelAction?.Enable();
        }
        void OnDisable()
        {
            chief.OnDialogue -= Show;
            hub.OnStationClosed -= HandleClosed;
            session.OnFinished -= HandleFinished;
        }
        void Show(VillageChief.DialogueView view)
        {
            active = true;
            offer = view.offer;
            SetChoice(false);
            eventSystem?.SetSelectedGameObject(null);
            session.Begin(view.speaker, view.lines, true);
        }
        void HandleFinished(int id, bool completed)
        {
            if (!active) return;
            if (!completed || offer == null) { Close(); return; }
            var text = new StringBuilder(string.Format(data.questTitleFormat, offer.title));
            if (!string.IsNullOrEmpty(offer.description)) text.Append('\n').Append(offer.description);
            string rewards = Describe(offer.rewards);
            if (rewards.Length > 0) text.Append('\n').Append(string.Format(data.questRewardsFormat, rewards));
            questText.text = text.ToString();
            SetChoice(true);
            eventSystem?.SetSelectedGameObject(acceptButton.gameObject);
        }
        string Describe(List<RewardData> rewards)
        {
            var text = new StringBuilder();
            if (rewards == null) return text.ToString();
            foreach (var reward in rewards)
            {
                string part = reward != null ? reward.Describe(data.rewardSkillPointsFormat) : string.Empty;
                if (string.IsNullOrEmpty(part)) continue;
                if (text.Length > 0) text.Append(data.rewardSeparator);
                text.Append(part);
            }
            return text.ToString();
        }
        void SetChoice(bool value)
        {
            choosing = value;
            choiceGroup.SetActive(value);
            choiceCanvas.alpha = value ? 1f : 0f;
            choiceCanvas.blocksRaycasts = choiceCanvas.interactable = value;
        }
        void Accept() { if (choosing && offer != null && !chief.Accept(offer)) Close(); }
        void Close() { if (active) hub.CloseStation(); }
        void HandleClosed(Interactable target)
        {
            if (target != chief) return;
            active = false;
            session.Hide();
            SetChoice(false);
            eventSystem?.SetSelectedGameObject(null);
        }
        void Update()
        {
            if (!active) return;
            if (cancelAction != null && cancelAction.WasPressedThisFrame()) { Close(); return; }
            if (choosing && eventSystem != null && eventSystem.currentSelectedGameObject == null)
                eventSystem.SetSelectedGameObject(acceptButton.gameObject);
        }
    }
}
