using BossFight.Player;
using BossFight.Progression;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.Hub
{
    /// <summary>
    /// 休息區的傳送門（T17 暫時版）：玩家站在觸發區裡按 Player/Interact（E、手把 Y）就呼叫 SceneFlow.GoToBattle 進 Boss 戰。
    /// 觸發區是同物件的 trigger Collider（大小由 HubBuilder 決定），只認玩家的 CharacterController（PlayerController 那一層），Hurtbox 不算。
    /// 用 WasPressedThisFrame，不受 Interact 的 Hold 互動影響（T20 換成正式的 Interactable，並把 Hold 拿掉）。沒有互動提示、沒有挑戰清單。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class Portal : MonoBehaviour
    {
        [SerializeField] SceneFlow sceneFlow;

        InputAction interactAction;
        int playersInside;

        void Awake()
        {
            var actions = InputSystem.actions;
            if (actions != null) interactAction = actions.FindAction("Player/Interact", true);
            else Debug.LogError("[Portal] 找不到 Project-wide Input Actions，傳送門不能用。", this);
        }

        void OnEnable() => interactAction?.Enable();

        void Update()
        {
            if (playersInside <= 0 || interactAction == null || !interactAction.WasPressedThisFrame()) return;
            if (sceneFlow != null) sceneFlow.GoToBattle();
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other)) playersInside++;
        }

        void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other)) playersInside = Mathf.Max(0, playersInside - 1);
        }

        static bool IsPlayer(Collider other) => other is CharacterController && other.GetComponent<PlayerController>() != null;
    }
}
