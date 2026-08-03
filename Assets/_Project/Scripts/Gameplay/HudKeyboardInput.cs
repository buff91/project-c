using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectC.Gameplay
{
    /// <summary>HUD 컨트롤러가 공유하는 키보드 명령. 월드 이동·포인터 입력은 소유하지 않는다.</summary>
    internal enum HudKeyboardAction
    {
        Cancel = 0,
        ToggleInventory = 1,
        ToggleDebugPanel = 2
    }

    /// <summary>
    /// Input System/legacy 차이를 HUD 액션으로 번역한다. 소비자는 키나 플랫폼 API를 직접 읽지 않는다.
    /// </summary>
    internal static class HudKeyboardInput
    {
        internal static bool WasPressedThisFrame(HudKeyboardAction action)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;

            switch (action)
            {
                case HudKeyboardAction.Cancel:
                    return keyboard.escapeKey.wasPressedThisFrame;
                case HudKeyboardAction.ToggleInventory:
                    return keyboard.iKey.wasPressedThisFrame;
                case HudKeyboardAction.ToggleDebugPanel:
                    if (keyboard.f1Key.wasPressedThisFrame) return true;
                    bool modifier = keyboard.leftCommandKey.isPressed ||
                                    keyboard.rightCommandKey.isPressed ||
                                    keyboard.leftCtrlKey.isPressed ||
                                    keyboard.rightCtrlKey.isPressed;
                    return modifier && keyboard.dKey.wasPressedThisFrame;
                default:
                    return false;
            }
#else
            switch (action)
            {
                case HudKeyboardAction.Cancel:
                    return Input.GetKeyDown(KeyCode.Escape);
                case HudKeyboardAction.ToggleInventory:
                    return Input.GetKeyDown(KeyCode.I);
                case HudKeyboardAction.ToggleDebugPanel:
                    if (Input.GetKeyDown(KeyCode.F1)) return true;
                    bool modifier = Input.GetKey(KeyCode.LeftCommand) ||
                                    Input.GetKey(KeyCode.RightCommand) ||
                                    Input.GetKey(KeyCode.LeftControl) ||
                                    Input.GetKey(KeyCode.RightControl);
                    return modifier && Input.GetKeyDown(KeyCode.D);
                default:
                    return false;
            }
#endif
        }
    }
}
