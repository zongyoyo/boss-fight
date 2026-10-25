using BossFight.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>
    /// 休息區的站點（T20）：物件的位置是互動點，玩家的水平距離在 HubLayoutData 的 interactRadius 內時 InteractionScanner 會選到它。
    /// 按 Player/Interact 後 HubManager 打開介面：玩家走到 standPoint、面向互動點，stationCamera 提高優先度。
    /// 介面開著時 HubManager 依序呼叫 Opened → 每幀 Tick（arrived = 走到站位點了）→ Closed，子類別覆寫來做站點自己的事（營火坐下）。
    /// 傳送門是子類別 Portal、營火是 Campfire（T21）；鐵匠鋪、村長在 T22–T24 換成各自的子類別。由 HubBuilder 建立與接線。
    /// </summary>
    [DisallowMultipleComponent]
    public class Interactable : MonoBehaviour
    {
        [SerializeField] HubLayoutData layout;
        [SerializeField] HubStationKind kind;
        [Tooltip("打開介面時玩家走到這裡、面向互動點")]
        [SerializeField] Transform standPoint;
        [Tooltip("介面開著時的鏡頭（平常優先度最低，HubManager 打開介面時調高）")]
        [SerializeField] CinemachineCamera stationCamera;

        public HubStationKind Kind => kind;
        public Transform StandPoint => standPoint != null ? standPoint : transform;
        public CinemachineCamera StationCamera => stationCamera;

        /// <summary>可以互動的水平距離 (m)；版面裡沒有這個站點時是 0。</summary>
        public float InteractRadius
        {
            get
            {
                var station = layout != null ? layout.Get(kind) : null;
                return station != null ? station.interactRadius : 0f;
            }
        }

        /// <summary>position 在互動範圍內時回傳 true，並給出水平距離的平方。</summary>
        public bool InRange(Vector3 position, out float sqrDistance)
        {
            Vector3 d = position - transform.position;
            sqrDistance = d.x * d.x + d.z * d.z;
            float r = InteractRadius;
            return sqrDistance <= r * r;
        }

        /// <summary>HubManager 打開這個站點時呼叫（鏡頭已切過去、OnStationOpened 之前）。</summary>
        public virtual void Opened(PlayerRig rig) { }

        /// <summary>介面開著時每幀呼叫；arrived = 玩家已經走到站位點。介面開著時換了角色，rig 就是新的角色。</summary>
        public virtual void Tick(PlayerRig rig, bool arrived) { }

        /// <summary>HubManager 關掉這個站點時呼叫（OnStationClosed 之前）。</summary>
        public virtual void Closed(PlayerRig rig) { }
    }
}
