using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 演出交給上層的提示點。SequenceEnd 是正常播完的標記（CutsceneDirector 自己處理），其他由上層流程（PrologueCutsceneFlow）
    /// 指揮對白與角色；Cutscene 不引用上層。新增種類往後加，不要插在中間（資產存的是數字）。
    /// </summary>
    public enum CutsceneCue
    {
        None,
        SequenceEnd,
        TransitionLine,
        Power,
        Farewell,
        FlowerEmerge,
        Surprise,
        FlowerBurrow,
        /// <summary>序章開場：中央土堆動一下（菊花還藏在土裡）。</summary>
        MoundPeek,
    }

    /// <summary>Timeline 標記：播到時把 kind 交給 CutsceneDirector.OnCue。</summary>
    public class CutsceneCueMarker : Marker, INotification, INotificationOptionProvider, ISerializationCallbackReceiver
    {
        [Tooltip("交給上層的提示種類")] public CutsceneCue kind;
        // 舊版存字串；讀到時轉成 kind（重新存檔後就清空）
        [SerializeField, HideInInspector] string cue;

        public PropertyName id => new PropertyName(nameof(CutsceneCueMarker));
        public NotificationFlags flags => NotificationFlags.TriggerOnce;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(cue)) return;
            if (kind == CutsceneCue.None && Enum.TryParse(cue, out CutsceneCue parsed)) kind = parsed;
            cue = null;
        }
    }
}
