using NUnit.Framework;
using ProjectC.Gameplay;
using UnityEngine.InputSystem;

namespace ProjectC.Tests.PlayMode
{
    public sealed class HudKeyboardInputPlayModeTests : InputTestFixture
    {
        private static readonly HudKeyboardAction[] AllActions =
        {
            HudKeyboardAction.Cancel,
            HudKeyboardAction.ToggleInventory,
            HudKeyboardAction.ToggleDebugPanel
        };

        [Test]
        public void NoKeyboard_ReturnsFalseForEveryAction()
        {
            foreach (HudKeyboardAction action in AllActions)
                Assert.IsFalse(HudKeyboardInput.WasPressedThisFrame(action));
        }

        [TestCase(Key.Escape)]
        [TestCase(Key.I)]
        [TestCase(Key.F1)]
        public void SingleKey_EmitsOnlyMappedActionForOneFrame(Key key)
        {
            HudKeyboardAction expected = key == Key.Escape
                ? HudKeyboardAction.Cancel
                : key == Key.I
                    ? HudKeyboardAction.ToggleInventory
                    : HudKeyboardAction.ToggleDebugPanel;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard[key]);

            foreach (HudKeyboardAction action in AllActions)
                Assert.AreEqual(
                    action == expected,
                    HudKeyboardInput.WasPressedThisFrame(action),
                    action.ToString());

            InputSystem.Update();
            Assert.IsFalse(HudKeyboardInput.WasPressedThisFrame(expected),
                "Held keys must not repeat without a new press edge.");
        }

        [TestCase(Key.LeftCtrl)]
        [TestCase(Key.RightCtrl)]
        [TestCase(Key.LeftCommand)]
        [TestCase(Key.RightCommand)]
        public void DebugChord_AcceptsEachCommandOrControlModifier(Key modifierKey)
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard[modifierKey]);
            Assert.IsFalse(
                HudKeyboardInput.WasPressedThisFrame(HudKeyboardAction.ToggleDebugPanel),
                "A modifier alone must not toggle the panel.");

            Press(keyboard.dKey);
            Assert.IsTrue(
                HudKeyboardInput.WasPressedThisFrame(HudKeyboardAction.ToggleDebugPanel));

            InputSystem.Update();
            Assert.IsFalse(
                HudKeyboardInput.WasPressedThisFrame(HudKeyboardAction.ToggleDebugPanel),
                "Holding the chord must not toggle again.");
        }

        [Test]
        public void PlainD_DoesNotToggleDebugPanel()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.dKey);

            Assert.IsFalse(
                HudKeyboardInput.WasPressedThisFrame(HudKeyboardAction.ToggleDebugPanel));
        }

        [Test]
        public void DebugChord_DPressedBeforeModifier_DoesNotToggleDebugPanel()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.dKey);
            Assert.IsFalse(
                HudKeyboardInput.WasPressedThisFrame(HudKeyboardAction.ToggleDebugPanel));

            InputSystem.Update();
            Press(keyboard.leftCtrlKey);
            Assert.IsFalse(
                HudKeyboardInput.WasPressedThisFrame(HudKeyboardAction.ToggleDebugPanel),
                "The D press edge must happen while a modifier is already held.");
        }
    }
}
