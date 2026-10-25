using System;
using System.Collections.Generic;
using BossFight.Player;
using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>
    /// 每幀從站點清單裡找玩家互動範圍內最近的一個（Interactable.InRange，只看水平距離），換了就送 OnFocusChanged（離開範圍時送 null）。
    /// 站點清單由 HubBuilder 接好（執行期不找物件）；玩家由 PlayerSpawner 交給 BindPlayer，換角色時直接換引用（沒有訂閱玩家的事件）。
    /// 互動提示（InteractionPromptUI）與 HubManager 訂閱 OnFocusChanged。
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionScanner : MonoBehaviour, IPlayerBinding
    {
        [SerializeField] List<Interactable> interactables = new List<Interactable>();

        /// <summary>參數：範圍內最近的站點；離開所有站點的範圍時是 null。</summary>
        public event Action<Interactable> OnFocusChanged;

        public Interactable Focus { get; private set; }

        Transform player;

        public void BindPlayer(PlayerRig rig) => player = rig != null ? rig.transform : null;

        void OnDisable() => SetFocus(null);

        void Update()
        {
            Interactable best = null;
            if (player != null)
            {
                float bestSqr = float.PositiveInfinity;
                foreach (var target in interactables)
                {
                    if (target == null || !target.isActiveAndEnabled || !target.InRange(player.position, out float sqr) || sqr >= bestSqr) continue;
                    best = target;
                    bestSqr = sqr;
                }
            }
            SetFocus(best);
        }

        void SetFocus(Interactable target)
        {
            if (target == Focus) return;
            Focus = target;
            OnFocusChanged?.Invoke(target);
        }
    }
}
