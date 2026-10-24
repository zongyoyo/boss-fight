using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace BossFight.UI
{
    /// <summary>
    /// 判斷按鍵提示要顯示鍵鼠還是手把的鍵：最後一次輸入是鍵盤按鍵、滑鼠按鍵或移動滑鼠 → 鍵鼠；手把任一鍵或推搖桿 → 手把。啟動時是鍵鼠。
    /// 放在 GameOptions 物件上；處決、啃骨頭、跳過、選單返回的提示在 Inspector 引用這裡，訂閱 OnDeviceChanged 換字。
    /// Start 時送一次初始值；改鍵（InputSystem 的綁定改變）後也會再送一次同樣的值，讓提示重讀目前的鍵。時間用真實時間，暫停中照常判斷。
    /// 這裡的數字全是輸入偵測的門檻，不是玩法數值。
    /// </summary>
    [DisallowMultipleComponent]
    public class InputHintDevice : MonoBehaviour
    {
        [Tooltip("滑鼠一幀移動超過這麼多像素才算在用鍵鼠（避免手把玩家碰到滑鼠就換）")]
        [Min(0f)] [SerializeField] float mouseMoveThreshold = 4f;
        [Tooltip("搖桿推超過這個比例才算在用手把（避免搖桿飄移）")]
        [Range(0f, 1f)] [SerializeField] float stickThreshold = 0.5f;

        /// <summary>參數：true = 手把，false = 鍵鼠。</summary>
        public event Action<bool> OnDeviceChanged;

        public bool UsingGamepad { get; private set; }

        /// <summary>目前裝置在 InputSystem_Actions 裡的控制組名稱（BindingText.KeyboardGroup／GamepadGroup）。</summary>
        public string Group => BindingText.Group(UsingGamepad);

        IDisposable buttonListener;
        bool bindingsDirty;

        void OnEnable()
        {
            buttonListener = InputSystem.onAnyButtonPress.Call(HandleButton);
            InputSystem.onActionChange += HandleActionChange;
        }

        void OnDisable()
        {
            buttonListener?.Dispose();
            buttonListener = null;
            InputSystem.onActionChange -= HandleActionChange;
        }

        void Start() => OnDeviceChanged?.Invoke(UsingGamepad);

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > mouseMoveThreshold * mouseMoveThreshold) SetGamepad(false);
            var gamepad = Gamepad.current;
            if (gamepad != null && (gamepad.leftStick.ReadValue().magnitude > stickThreshold ||
                                    gamepad.rightStick.ReadValue().magnitude > stickThreshold))
                SetGamepad(true);

            if (!bindingsDirty) return;
            bindingsDirty = false;
            OnDeviceChanged?.Invoke(UsingGamepad);
        }

        void HandleButton(InputControl control)
        {
            var device = control.device;
            if (device is Gamepad) SetGamepad(true);
            else if (device is Keyboard || device is Mouse) SetGamepad(false);
        }

        // 改鍵、恢復預設：同一幀可能改很多個，下一個 Update 再送一次
        void HandleActionChange(object target, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged) bindingsDirty = true;
        }

        void SetGamepad(bool gamepad)
        {
            if (UsingGamepad == gamepad) return;
            UsingGamepad = gamepad;
            OnDeviceChanged?.Invoke(gamepad);
        }
    }
}
