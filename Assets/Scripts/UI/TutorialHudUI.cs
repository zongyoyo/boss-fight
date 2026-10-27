using System.Collections.Generic;
using BossFight.Battle;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>事件式操作提示；不攔輸入，改鍵與裝置切換時重建文字。</summary>
    public class TutorialHudUI : MonoBehaviour
    {
        [SerializeField] TutorialDirector director;
        [SerializeField] TutorialUIData data;
        [SerializeField] InputHintDevice hintDevice;
        [SerializeField] InputNameData inputNames;
        [SerializeField] CanvasGroup group;
        [SerializeField] Text label;
        TutorialView view;
        bool gamepad;
        float alpha;
        void OnEnable()
        {
            director.OnViewChanged += Show;
            if (hintDevice != null) { hintDevice.OnDeviceChanged += HandleDevice; gamepad = hintDevice.UsingGamepad; }
            director.PublishState();
        }
        void OnDisable()
        {
            director.OnViewChanged -= Show;
            if (hintDevice != null) hintDevice.OnDeviceChanged -= HandleDevice;
        }
        void Show(TutorialView next)
        {
            bool wasComplete = view.completed;
            view = next;
            if (!next.visible) alpha = 0f;
            else if (!next.completed || !wasComplete) alpha = 1f;
            Render();
        }
        void HandleDevice(bool value) { gamepad = value; Render(); }
        void Render()
        {
            group.interactable = group.blocksRaycasts = false;
            group.alpha = alpha;
            if (!string.IsNullOrEmpty(view.interactionHint))
            {
                label.text = string.Format(view.interactionHint, BindingText.FirstDisplay("Player/Interact", BindingText.Group(gamepad), inputNames));
                label.color = data.textColor;
                return;
            }
            if (view.step == null) { label.text = string.Empty; return; }
            string keys = BindingText.FirstDisplay(view.step.actionPath, BindingText.Group(gamepad), inputNames);
            if (view.step.condition == TutorialCondition.Move && !gamepad)
            {
                var action = InputSystem.actions.FindAction(view.step.actionPath, false);
                var parts = new List<string>();
                if (action != null)
                    foreach (string part in new[] { "up", "down", "left", "right" })
                        parts.Add(BindingText.Display(action, BindingText.FindBinding(action, BindingText.Group(false), part, 0), inputNames));
                keys = string.Join(data.bindingSeparator, parts);
            }
            label.text = (view.completed ? data.checkMark + " " : string.Empty) + string.Format(view.step.hintFormat, keys) +
                string.Format(data.progressFormat, view.progress, view.step.target);
            label.color = view.completed ? data.completeColor : data.textColor;
        }
        void Update()
        {
            if (!view.completed || !view.visible) return;
            alpha = data.fadeTime > 0f ? Mathf.MoveTowards(alpha, 0f, Time.deltaTime / data.fadeTime) : 0f;
            group.alpha = alpha;
        }
    }
}
