using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>演出時間軸上的事件種類。</summary>
    public enum CutsceneEvent
    {
        /// <summary>顯示字卡（BOSS FIGHT + Boss 名稱）；CutsceneDirector 轉成 OnTitleCard。</summary>
        TitleCard,
        /// <summary>長按跳過時跳到這個時間點（沒有這個標記就跳到結尾）。只是時間點，不會送出通知。</summary>
        SkipTarget,
    }

    /// <summary>
    /// 放在 Timeline 最上面 Marker 列的事件標記。播放經過時通知 PlayableDirector 同一個物件上的 CutsceneDirector；
    /// 設成 Retroactive，跳過開場直接跳過這個時間點時也會補送。
    /// </summary>
    public class CutsceneEventMarker : Marker, INotification, INotificationOptionProvider
    {
        [Tooltip("事件種類")]
        public CutsceneEvent kind = CutsceneEvent.TitleCard;
        [Tooltip("字卡顯示多久（秒），之後淡出")]
        [Min(0f)] public float duration = 1.5f;

        public PropertyName id => new PropertyName(nameof(CutsceneEventMarker));

        public NotificationFlags flags => NotificationFlags.Retroactive | NotificationFlags.TriggerOnce;
    }
}
