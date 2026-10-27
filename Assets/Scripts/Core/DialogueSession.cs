using System;
using System.Collections.Generic;
using UnityEngine;

namespace BossFight.Core
{
    /// <summary>共用對話畫面快照；不攜帶村長、任務或 UI 元件。</summary>
    public readonly struct DialogueView
    {
        public readonly int id, index;
        public readonly string speaker, line;
        public readonly bool visible, cancellable, advanceable;
        public DialogueView(int id, int index, string speaker, string line, bool visible, bool cancellable, bool advanceable = true)
        { this.id = id; this.index = index; this.speaker = speaker; this.line = line; this.visible = visible; this.cancellable = cancellable; this.advanceable = advanceable; }
    }

    /// <summary>每場景一份的對話管道；流程發請求，UI 回報前進，只有純文字與事件。</summary>
    [DisallowMultipleComponent]
    public class DialogueSession : MonoBehaviour
    {
        public event Action<DialogueView> OnViewChanged;
        public event Action<int, bool> OnFinished;
        readonly List<string> lines = new List<string>();
        string speaker;
        int id, index;
        bool visible, cancellable, advanceable;

        public int Begin(string name, IReadOnlyList<string> text, bool allowCancel, bool allowAdvance = true)
        {
            id++;
            speaker = name;
            lines.Clear();
            if (text != null) foreach (string line in text) if (!string.IsNullOrEmpty(line)) lines.Add(line);
            index = 0;
            cancellable = allowCancel;
            advanceable = allowAdvance;
            visible = lines.Count > 0;
            PublishState();
            if (!visible) OnFinished?.Invoke(id, true);
            return id;
        }

        public void Advance(int requestId)
        {
            if (!visible || !advanceable || id != requestId) return;
            index++;
            if (index >= lines.Count) Finish(true);
            else PublishState();
        }
        public void Cancel(int requestId)
        {
            if (visible && cancellable && id == requestId) Finish(false);
        }
        /// <summary>站點關閉或流程卸載時靜默收起，不冒充對話完成。</summary>
        public void Hide() { visible = false; PublishState(); }
        void Finish(bool completed)
        {
            int finishedId = id;
            visible = false;
            PublishState();
            OnFinished?.Invoke(finishedId, completed);
        }
        public void PublishState() => OnViewChanged?.Invoke(new DialogueView(id, index, speaker,
            visible ? lines[index] : string.Empty, visible, cancellable, advanceable));
    }
}
