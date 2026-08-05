using System.Reflection;
using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ProjectC.Tests.PlayMode
{
    public sealed class IsoTapInputBlockerPlayModeTests : InputTestFixture
    {
        private static readonly MethodInfo UpdateMethod = typeof(IsoTapInput).GetMethod(
            "Update",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void WorldCommandBlocker_SuppressesEveryWorldCommand()
        {
            var root = new GameObject("IsoTapInput blocker test");
            try
            {
                GridManager grid = root.AddComponent<GridManager>();
                grid.buildDemoOnStart = false;
                var target = new GridPos(1, 1, 0);
                grid.Map.Set(target, TileKind.Floor);

                IsoTapInput input = root.AddComponent<IsoTapInput>();
                input.TilePicker = _ => target;
                input.trackHover = true;

                int rotations = 0;
                int steps = 0;
                int interactions = 0;
                int waits = 0;
                int pans = 0;
                int recenters = 0;
                int taps = 0;
                int hoveredTiles = 0;
                input.ViewRotationRequested += _ => rotations++;
                input.StepRequested += (_, _) => steps++;
                input.InteractRequested += () => interactions++;
                input.WaitRequested += () => waits++;
                input.CameraPanRequested += _ => pans++;
                input.CameraRecenterRequested += () => recenters++;
                input.TileTapped += (_, _) => taps++;
                input.TileHovered += position =>
                {
                    if (position.HasValue) hoveredTiles++;
                };
                input.WorldCommandBlocker = () => true;

                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Mouse mouse = InputSystem.AddDevice<Mouse>();

                PressAndUpdate(input, keyboard.qKey);
                PressAndUpdate(input, keyboard.wKey);
                PressAndUpdate(input, keyboard.spaceKey);
                PressAndUpdate(input, keyboard.xKey);
                PressAndUpdate(input, keyboard.homeKey);

                Set(mouse.position, new Vector2(100f, 100f));
                Press(mouse.middleButton);
                InvokeUpdate(input);
                Set(mouse.position, new Vector2(140f, 115f));
                InvokeUpdate(input);
                Release(mouse.middleButton);

                Press(mouse.leftButton);
                InvokeUpdate(input);
                Release(mouse.leftButton);

                Assert.Zero(rotations, "Q/E 회전이 모달 뒤 월드로 새면 안 된다");
                Assert.Zero(steps, "WASD/방향키 이동이 모달 뒤 월드로 새면 안 된다");
                Assert.Zero(interactions, "Space 상호작용이 모달 뒤 월드로 새면 안 된다");
                Assert.Zero(waits, "X 대기가 모달 뒤 월드로 새면 안 된다");
                Assert.Zero(pans, "중클릭 팬이 모달 뒤 월드로 새면 안 된다");
                Assert.Zero(recenters, "Home 복귀가 모달 뒤 월드로 새면 안 된다");
                Assert.Zero(taps, "왼쪽 클릭이 모달 뒤 타일 행동으로 새면 안 된다");
                Assert.Zero(hoveredTiles, "포인터 호버가 모달 뒤 월드로 새면 안 된다");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BlockingDuringPan_CancelsGestureUntilMiddleButtonIsPressedAgain()
        {
            var root = new GameObject("IsoTapInput pan cancellation test");
            try
            {
                GridManager grid = root.AddComponent<GridManager>();
                grid.buildDemoOnStart = false;
                IsoTapInput input = root.AddComponent<IsoTapInput>();
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                int pans = 0;
                input.CameraPanRequested += _ => pans++;

                Set(mouse.position, new Vector2(80f, 80f));
                Press(mouse.middleButton);
                InvokeUpdate(input);
                Set(mouse.position, new Vector2(110f, 80f));
                InvokeUpdate(input);
                Assert.AreEqual(1, pans, "차단 전에는 정상 팬 제스처여야 한다");

                input.WorldCommandBlocker = () => true;
                Set(mouse.position, new Vector2(140f, 80f));
                InvokeUpdate(input);
                Assert.AreEqual(1, pans, "차단 진입 프레임이 진행 중 팬을 끊어야 한다");

                input.WorldCommandBlocker = null;
                Set(mouse.position, new Vector2(170f, 80f));
                InvokeUpdate(input);
                Assert.AreEqual(
                    1,
                    pans,
                    "모달을 닫아도 누르고 있던 버튼으로 팬이 자동 재개되면 안 된다");

                Release(mouse.middleButton);
                Press(mouse.middleButton);
                InvokeUpdate(input);
                Set(mouse.position, new Vector2(200f, 80f));
                InvokeUpdate(input);
                Assert.AreEqual(2, pans, "버튼을 새로 누르면 팬을 다시 시작할 수 있어야 한다");
                Release(mouse.middleButton);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Blocking_ClearsExistingHoverAndDoesNotRefreshItWhileOpen()
        {
            var root = new GameObject("IsoTapInput hover cancellation test");
            try
            {
                GridManager grid = root.AddComponent<GridManager>();
                grid.buildDemoOnStart = false;
                var target = new GridPos(1, 1, 0);
                grid.Map.Set(target, TileKind.Floor);

                IsoTapInput input = root.AddComponent<IsoTapInput>();
                input.trackHover = true;
                input.TilePicker = _ => target;
                int entered = 0;
                int cleared = 0;
                input.TileHovered += position =>
                {
                    if (position.HasValue) entered++;
                    else cleared++;
                };

                Mouse mouse = InputSystem.AddDevice<Mouse>();
                Set(mouse.position, new Vector2(80f, 80f));
                InvokeUpdate(input);
                Assert.AreEqual(1, entered, "차단 전에는 타일 호버를 정상 게시해야 한다");

                input.WorldCommandBlocker = () => true;
                InvokeUpdate(input);
                Assert.AreEqual(1, cleared, "모달 진입 시 남아 있던 월드 호버를 지워야 한다");

                Set(mouse.position, new Vector2(120f, 100f));
                InvokeUpdate(input);
                Assert.AreEqual(1, entered, "모달이 열린 동안 새 월드 호버를 게시하면 안 된다");
                Assert.AreEqual(1, cleared, "이미 지운 호버를 매 프레임 중복 해제하면 안 된다");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private void PressAndUpdate(
            IsoTapInput input,
            ButtonControl button)
        {
            Press(button);
            InvokeUpdate(input);
            Release(button);
        }

        private static void InvokeUpdate(IsoTapInput input)
        {
            Assert.NotNull(UpdateMethod);
            UpdateMethod.Invoke(input, null);
        }
    }
}
