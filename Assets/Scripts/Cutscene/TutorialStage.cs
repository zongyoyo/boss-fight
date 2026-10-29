using Unity.Cinemachine;
using UnityEngine;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 教學對白的鏡頭（H2）：兩台 CinemachineCamera 輪流用，每次 Show 把下一台擺到 TutorialStagingData 的位置、優先度拉高，
    /// 另一台降回去，Brain 就在兩個構圖之間混合；Release 兩台都降回去，回到玩家的自由視角或鎖定鏡頭。
    /// 位置在 Show 的那一刻依角色位置算好，之後不跟著動。由 TutorialDirector 呼叫，只動鏡頭。
    /// </summary>
    public class TutorialStage : MonoBehaviour
    {
        [SerializeField] TutorialStagingData data;
        [SerializeField] CinemachineCamera[] cameras = new CinemachineCamera[2];
        [Tooltip("兩台鏡頭各自看的點（LookAt）")]
        [SerializeField] Transform[] lookPoints = new Transform[2];

        const int HiddenPriority = -100;
        int current = -1;
        TutorialShot shown;

        /// <summary>目前的鏡頭；None = 沒有在用。</summary>
        public TutorialShot Current => shown;

        void Awake()
        {
            foreach (var cam in cameras)
                if (cam != null) cam.Priority = HiddenPriority;
        }

        /// <summary>切到 shot 的構圖；已經是同一個鏡頭時不動。三個角色的引用可以是 null（用玩家代替）。</summary>
        public void Show(TutorialShot shot, Transform player, Transform mentor, Transform sapling)
        {
            if (data == null || shot == TutorialShot.None || player == null) { Release(); return; }
            if (shot == shown) return;
            var setting = data.Get(shot);
            if (setting == null) return;
            Vector3 origin = Point(setting.anchor, player, mentor, sapling);
            Vector3 forward = Point(setting.frameTo, player, mentor, sapling) - Point(setting.frameFrom, player, mentor, sapling);
            forward.y = 0f;
            // frameFrom 與 frameTo 一樣時（例如只看小樹精）改用玩家看過去的方向
            if (forward.sqrMagnitude < 1e-4f) forward = origin - player.position;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = player.forward;
            var frame = Quaternion.LookRotation(forward.normalized);
            int next = current == 0 ? 1 : 0;
            var cam = cameras[next];
            cam.transform.position = origin + frame * setting.offset;
            lookPoints[next].position = Point(setting.lookAt, player, mentor, sapling) + frame * setting.lookOffset;
            cam.transform.rotation = Quaternion.LookRotation(lookPoints[next].position - cam.transform.position);
            var lens = cam.Lens;
            lens.FieldOfView = setting.fieldOfView;
            cam.Lens = lens;
            cam.Priority = data.priority;
            if (current >= 0 && current != next) cameras[current].Priority = HiddenPriority;
            current = next;
            shown = shot;
        }

        /// <summary>收掉對白鏡頭，回到玩家的鏡頭。</summary>
        public void Release()
        {
            foreach (var cam in cameras)
                if (cam != null) cam.Priority = HiddenPriority;
            current = -1;
            shown = TutorialShot.None;
        }

        static Vector3 Point(ShotAnchor anchor, Transform player, Transform mentor, Transform sapling)
        {
            Vector3 p = player.position;
            Vector3 m = mentor != null ? mentor.position : p;
            Vector3 s = sapling != null ? sapling.position : m;
            switch (anchor)
            {
                case ShotAnchor.Mentor: return m;
                case ShotAnchor.Sapling: return s;
                case ShotAnchor.PlayerMentor: return (p + m) * 0.5f;
                case ShotAnchor.MentorSapling: return (m + s) * 0.5f;
                case ShotAnchor.PlayerSapling: return (p + s) * 0.5f;
                default: return p;
            }
        }
    }
}
