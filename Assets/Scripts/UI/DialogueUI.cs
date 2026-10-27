using BossFight.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>共用逐字對話框；只訂閱文字快照，不依賴村長與休息區管理者。</summary>
    public class DialogueUI : MonoBehaviour
    {
        [SerializeField] HubUIData data;
        [SerializeField] DialogueSession session;
        [SerializeField] GamePause pause;
        [SerializeField] CanvasGroup group;
        [SerializeField] Text speakerText;
        [SerializeField] Text bodyText;
        [SerializeField] Text continueMark;
        InputAction submitAction, clickAction, interactAction;
        DialogueView view;
        int shownFrame;
        float shownChars;
        Vector2 continueBase;

        void Awake()
        {
            var actions = InputSystem.actions;
            submitAction = actions?.FindAction("UI/Submit", false);
            clickAction = actions?.FindAction("UI/Click", false);
            interactAction = actions?.FindAction("Player/Interact", false);
            continueBase = continueMark.rectTransform.anchoredPosition;
            continueMark.text = data.dialogueContinueMark;
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;
        }
        void OnEnable()
        {
            submitAction?.Enable(); clickAction?.Enable(); interactAction?.Enable();
            session.OnViewChanged += Show;
            if (pause != null) pause.OnPausedChanged += HandlePause;
            session.PublishState();
        }
        void Start() => session.PublishState();
        void OnDisable()
        {
            session.OnViewChanged -= Show;
            if (pause != null) pause.OnPausedChanged -= HandlePause;
        }
        // 關閉選單用的確認鍵不能同幀跳掉一句台詞。
        void HandlePause(bool paused) => shownFrame = Time.frameCount;
        void Show(DialogueView next)
        {
            // 重送快照不重置逐字進度，也不丟失正在顯示的第一句。
            bool changed = next.id != view.id || next.index != view.index || next.visible != view.visible;
            view = next;
            group.alpha = next.visible ? 1f : 0f;
            group.blocksRaycasts = group.interactable = next.visible && next.advanceable;
            if (!changed) return;
            speakerText.text = next.speaker;
            shownChars = 0f;
            shownFrame = Time.frameCount;
            Render();
        }
        void Render()
        {
            string line = view.line ?? string.Empty;
            int count = Mathf.Min(line.Length, Mathf.FloorToInt(shownChars));
            bodyText.text = count >= line.Length ? line : line.Substring(0, count);
            continueMark.enabled = view.visible && view.advanceable && count >= line.Length;
        }
        void Update()
        {
            if (!view.visible || (pause != null && pause.IsPaused)) return;
            shownChars += data.dialogueCharsPerSecond * Time.deltaTime;
            Render();
            if (!view.advanceable) return;
            if (continueMark.enabled)
                continueMark.rectTransform.anchoredPosition = continueBase + Vector2.up *
                    (Mathf.Abs(Mathf.Sin(Time.unscaledTime * data.dialogueContinueRate * Mathf.PI)) * data.dialogueContinueBob);
            if (Time.frameCount == shownFrame) return;
            bool pressed = (submitAction != null && submitAction.WasPressedThisFrame()) ||
                (clickAction != null && clickAction.WasPressedThisFrame()) ||
                (interactAction != null && interactAction.WasPressedThisFrame());
            if (!pressed) return;
            if (!continueMark.enabled) { shownChars = view.line.Length; Render(); }
            else session.Advance(view.id);
        }
    }
}
