using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
namespace BossFight.Cutscene
{
    /// <summary>通用文字提示。上層依 cue 指揮對白與角色，不讓 Cutscene 引用上層流程。</summary>
    public class CutsceneCueMarker : Marker, INotification, INotificationOptionProvider
    {
        [Tooltip("交給上層的事件名稱") ] public string cue;
        public PropertyName id => new PropertyName(nameof(CutsceneCueMarker));
        public NotificationFlags flags => NotificationFlags.TriggerOnce;
    }
}
