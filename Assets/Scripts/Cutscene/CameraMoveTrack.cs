using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BossFight.Cutscene
{
    /// <summary>移動一台演出鏡頭（綁定它的 Transform）；開場第 1 鏡的高空環繞、第 2 鏡的緩降。</summary>
    [TrackColor(0.3f, 0.75f, 0.9f)]
    [TrackClipType(typeof(CameraMoveClip))]
    [TrackBindingType(typeof(Transform))]
    public class CameraMoveTrack : TrackAsset
    {
        // 讓 Timeline 視窗的編輯模式預覽結束時還原鏡頭位置
        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            var binding = director.GetGenericBinding(this) as Transform;
            if (binding != null)
            {
                driver.AddFromName<Transform>(binding.gameObject, "m_LocalPosition.x");
                driver.AddFromName<Transform>(binding.gameObject, "m_LocalPosition.y");
                driver.AddFromName<Transform>(binding.gameObject, "m_LocalPosition.z");
            }
            base.GatherProperties(director, driver);
        }

        protected override Playable CreatePlayable(PlayableGraph graph, GameObject gameObject, TimelineClip clip)
        {
            var playable = base.CreatePlayable(graph, gameObject, clip);
            if (playable.IsValid() && playable.GetPlayableType() == typeof(CameraMoveBehaviour))
                ((ScriptPlayable<CameraMoveBehaviour>)playable).GetBehaviour().clipDuration = clip.duration;
            return playable;
        }
    }
}
