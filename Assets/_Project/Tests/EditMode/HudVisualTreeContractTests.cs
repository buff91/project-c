using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ProjectC.Tests
{
    public class HudVisualTreeContractTests
    {
        private static readonly string[] RequiredNames =
        {
            "hud-root",
            "hp-hearts",
            "hp-value",
            "floor-instrument",
            "minimap-view",
            "minimap-floor-badge",
            "minimap-north-label",
            "minimap-player-marker",
            "settings-button",
            "game-menu-button",
            "rotate-left",
            "rotate-right",
            "vertical-view-controls",
            "vertical-view-up",
            "vertical-view-current",
            "vertical-view-down",
            "vertical-view-state",
            "view-label",
            "depth-label",
            "depth-caption",
            "location-label",
            "hunger-label",
            "floor-label",
            // 640×360 재편에서 생긴 셋. 기존 name= 리네임은 없다 — 역할만 재배정했다.
            "status-chips",
            "floor-stack",
            "message-log",
            "feedback-chip",
            "status-label",
            "vertical-hint-chip",
            "vertical-hint-label",
            "vertical-route-discovery",
            "route-discovery-title",
            "route-discovery-detail",
            "route-discovery-close",
            "potion-button",
            "potion-count",
            "bomb-button",
            "bomb-count",
            "frost-button",
            "frost-count",
            "bag-button",
            "mode-button",
            "mode-label",
            "combat-button",
            "combat-icon",
            "combat-label",
            "wait-button",
            "interact-button",
            "interact-label",
            "settings-modal",
            "settings-scroll",
            "development-viewport",
            "viewport-mode",
            "viewport-resolution",
            "viewport-apply",
            "inventory-modal",
            "inventory-grid",
            "inventory-capacity",
            "inventory-detail-icon",
            "inventory-detail-name",
            "inventory-use",
            "game-menu-modal",
            "boss-panel",
            "boss-name",
            "boss-health-fill",
            "boss-health-value",
            "boss-objective",
            "exit-modal",
            "exit-title",
            "exit-desc",
            "exit-extract",
            "exit-advance",
            "action-wheel",
            "gameover-overlay",
            "menu-button"
        };

        private static readonly string[] HubRequiredNames =
        {
            "hub-root",
            "hub-settings-button",
            "hub-menu-button",
            "hub-dungeon-modal",
            "hub-dungeon-catacombs",
            "hub-dungeon-flooded",
            "hub-dungeon-loadout",
            "hub-dungeon-enter",
            "hub-menu-modal",
            "hub-menu-resume",
            "hub-menu-quit",
            "hub-shop-modal",
            "hub-stash-modal",
            "hub-stash-grid",
            "hub-stash-capacity",
            "hub-stash-detail-icon",
            "hub-loadout-grid",
            "hub-loadout-capacity",
            "hub-to-loadout",
            "hub-to-stash",
            // 기록실 — UXML 이름과 컨트롤러 바인딩이 어긋나면 모달이 조용히 안 열린다
            // (컨트롤러가 null 참조를 null-conditional 로 넘겨서 예외도 안 난다).
            "hub-codex-modal",
            "hub-codex-list",
            "hub-codex-count",
            "hub-codex-close"
        };

        private static readonly string[] MainMenuRequiredNames =
        {
            "main-menu-root",
            "main-story-slot",
            "main-start-button",
            "main-continue-button",
            "main-settings-button",
            "main-quit-button",
            "settings-modal"
        };

        [TestCase("Assets/_Project/UI/PrototypeHUD.Mobile.uxml")]
        [TestCase("Assets/_Project/UI/PrototypeHUD.Desktop.uxml")]
        public void Layout_ContainsControllerContract(string assetPath)
        {
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
            Assert.IsNotNull(asset, $"HUD layout missing: {assetPath}");

            TemplateContainer tree = asset.CloneTree();
            foreach (string elementName in RequiredNames)
                Assert.IsNotNull(tree.Q(elementName), $"{assetPath} missing #{elementName}");

            Assert.IsTrue(string.IsNullOrEmpty(tree.Q<Button>("settings-button").text));
            Assert.IsTrue(string.IsNullOrEmpty(tree.Q<Button>("game-menu-button").text));
            Assert.IsNotNull(tree.Q<VisualElement>(className: "ui-settings-icon"));
            Assert.IsNotNull(tree.Q<VisualElement>(className: "ui-menu-icon"));
            Assert.IsNotNull(tree.Q<VisualElement>(className: "ui-rotate-left-icon"));
            Assert.IsNotNull(tree.Q<VisualElement>(className: "ui-rotate-right-icon"));
            Assert.IsNotNull(tree.Q<VisualElement>(className: "ui-backpack-icon"));
            Assert.IsNotNull(tree.Q<VisualElement>(className: "ui-wait-icon"));
            Button minimapPlayerMarker = tree.Q<Button>("minimap-player-marker");
            Assert.IsNotNull(minimapPlayerMarker);
            Assert.AreEqual(PickingMode.Position, minimapPlayerMarker.pickingMode);
            StringAssert.Contains("카메라 복귀", minimapPlayerMarker.tooltip);
            AssertSubtreeIgnoresPicking(
                minimapPlayerMarker.Q<VisualElement>(className: "minimap-player-glyph"),
                "Minimap player glyph");
            Assert.IsFalse(tree.Q("feedback-chip").ClassListContains("is-open"));
            Assert.IsFalse(tree.Q("vertical-hint-chip").ClassListContains("is-open"));

            VisualElement discovery = tree.Q("vertical-route-discovery");
            Assert.AreEqual(PickingMode.Ignore, discovery.pickingMode);
            AssertSubtreeIgnoresPicking(
                discovery.Q<Label>("route-discovery-title"),
                "Discovery content");
            Assert.AreEqual(
                PickingMode.Ignore,
                discovery.Q<Button>("route-discovery-close").pickingMode,
                "Closed discovery cards must not leave an invisible hit target");
            AssertSubtreeIgnoresPicking(tree.Q("boss-panel"), "Boss panel");

            // 판이 끝난 뒤 착지점은 캠프 하나다. 던전 씬을 그대로 리로드하는 "다시 도전"을
            // 되살리면 방금 번 골드·해금을 못 쓰고 같은 조건으로 돌아가는 길이 다시 생긴다.
            Assert.AreEqual("캠프로 돌아가기", tree.Q<Button>("menu-button").text);
            Assert.IsNull(tree.Q<Button>("restart-button"));
        }

        [TestCase(0, 13, 7f)]
        [TestCase(6, 13, 50f)]
        [TestCase(12, 13, 93f)]
        [TestCase(-3, 13, 7f)]
        [TestCase(20, 13, 93f)]
        public void MinimapMarkerPercent_UsesTileCenterAndKeepsMarkerInsideViewport(
            int coordinate,
            int size,
            float expected)
        {
            Assert.AreEqual(
                expected,
                PrototypeHudController.MinimapMarkerPercent(coordinate, size),
                0.0001f);
        }

        [Test]
        public void SharedHudStyle_ExposesDesktopMinimapCameraRecenterMarker()
        {
            string stylePath = System.IO.Path.Combine(
                Application.dataPath,
                "_Project/UI/PrototypeHUD.uss");
            string style = System.IO.File.ReadAllText(stylePath);

            Assert.That(
                style,
                Does.Match(
                    @"\.hud-root\.hud-desktop\s+\.minimap-player-marker\s*\{[^}]*display:\s*flex;"),
                "The scene can load the shared HUD directly, so its PC class must reveal the marker.");
        }

        [TestCase(6, 13, 148f, 44f, 74f)]
        [TestCase(0, 13, 148f, 44f, 55.08f)]
        [TestCase(12, 13, 44f, 148f, 40.92f)]
        public void MinimapMarkerAxisPixels_AccountsForScaleToFitLetterbox(
            int coordinate,
            int size,
            float axisLength,
            float crossLength,
            float expected)
        {
            Assert.AreEqual(
                expected,
                PrototypeHudController.MinimapMarkerAxisPixels(
                    coordinate,
                    size,
                    axisLength,
                    crossLength),
                0.0001f);
        }

        [Test]
        public void MinimapMarkerAxisPixels_RejectsUnresolvedLayout()
        {
            Assert.IsTrue(float.IsNaN(
                PrototypeHudController.MinimapMarkerAxisPixels(6, 13, 0f, 44f)));
        }

        private static void AssertSubtreeIgnoresPicking(
            VisualElement element,
            string context)
        {
            Assert.IsNotNull(element, $"{context} missing");
            Assert.AreEqual(
                PickingMode.Ignore,
                element.pickingMode,
                $"{context} can block world input: {element.name}");
            foreach (VisualElement child in element.Children())
                AssertSubtreeIgnoresPicking(child, context);
        }

        [Test]
        public void HubLayout_ContainsControllerContract()
        {
            const string assetPath = "Assets/_Project/UI/HubHUD.uxml";
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
            Assert.IsNotNull(asset, $"HUD layout missing: {assetPath}");

            TemplateContainer tree = asset.CloneTree();
            foreach (string elementName in HubRequiredNames)
                Assert.IsNotNull(tree.Q(elementName), $"{assetPath} missing #{elementName}");

            Assert.IsTrue(string.IsNullOrEmpty(tree.Q<Button>("hub-settings-button").text));
            Assert.IsTrue(string.IsNullOrEmpty(tree.Q<Button>("hub-menu-button").text));
            Assert.IsNotNull(tree.Q<VisualElement>(className: "ui-settings-icon"));
            Assert.IsNotNull(tree.Q<VisualElement>(className: "ui-menu-icon"));
            StringAssert.Contains(
                "플레이 가능",
                tree.Q<Button>("hub-dungeon-flooded").text);
            Assert.IsFalse(
                tree.Q<Button>("hub-dungeon-flooded").ClassListContains("locked"));
        }

        [Test]
        public void MainMenuLayout_ContainsControllerContract()
        {
            const string assetPath = "Assets/_Project/UI/MainMenuHUD.uxml";
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
            Assert.IsNotNull(asset, $"HUD layout missing: {assetPath}");

            TemplateContainer tree = asset.CloneTree();
            foreach (string elementName in MainMenuRequiredNames)
                Assert.IsNotNull(tree.Q(elementName), $"{assetPath} missing #{elementName}");

            Assert.AreEqual("게임 시작", tree.Q<Button>("main-start-button").text);
            Assert.IsNotNull(tree.Q<VisualElement>(className: "ui-settings-icon"));
            Assert.AreEqual(
                "설정",
                tree.Q<Button>("main-settings-button")
                    .Q<Label>(className: "icon-label").text);
        }

        [Test]
        public void TorchstoneActionIcons_AreCrispThirtyTwoPixelSourceSprites()
        {
            string[] names =
            {
                "settings",
                "menu",
                "rotate-left",
                "rotate-right",
                "backpack",
                "wait",
                "melee",
                "ranged",
                "interact"
            };

            foreach (string name in names)
            {
                string path = $"Assets/_Project/Art/Runtime/ui-{name}.png";
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(icon, $"UI icon missing: {path}");
                Assert.AreEqual(32, icon.width, $"{path} width");
                Assert.AreEqual(32, icon.height, $"{path} height");
            }
        }

        [Test]
        public void FieldDeckDesktopIcons_AreNativeTwelvePixelSourceSprites()
        {
            string[] names =
            {
                "settings",
                "menu",
                "rotate-left",
                "rotate-right",
                "backpack",
                "wait",
                "melee",
                "ranged",
                "interact"
            };

            foreach (string name in names)
            {
                string path = $"Assets/_Project/Art/Runtime/ui-field-{name}.png";
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(icon, $"Field Deck icon missing: {path}");
                Assert.AreEqual(12, icon.width, $"{path} width");
                Assert.AreEqual(12, icon.height, $"{path} height");

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"Texture importer missing: {path}");
                Assert.AreEqual(FilterMode.Point, importer.filterMode, $"{path} filter");
                Assert.IsFalse(importer.mipmapEnabled, $"{path} mipmaps");
                Assert.AreEqual(
                    TextureImporterCompression.Uncompressed,
                    importer.textureCompression,
                    $"{path} compression");
            }
        }

        [TestCase("ui-action-hex", 72, 64)]
        [TestCase("ui-action-hex-hover", 72, 64)]
        public void ActionWheelFrames_AreCrispSwapReadySprites(
            string name,
            int expectedWidth,
            int expectedHeight)
        {
            string path = $"Assets/_Project/Art/Runtime/{name}.png";
            Texture2D frame = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(frame, $"Action wheel frame missing: {path}");
            Assert.AreEqual(expectedWidth, frame.width, $"{path} width");
            Assert.AreEqual(expectedHeight, frame.height, $"{path} height");
        }

        [TestCase(false, false, 10, 0, false)]
        [TestCase(true, true, 10, 0, false)]
        [TestCase(true, false, 5, 5, false)]
        [TestCase(true, false, 6, 5, true)]
        public void ActionWheel_IsVisibleOnlyWhileTabIsHeld(
            bool tabHeld,
            bool anyModalOpen,
            int frame,
            int blockedThroughFrame,
            bool expected)
        {
            Assert.AreEqual(
                expected,
                PrototypeHudController.ShouldShowActionWheel(
                    tabHeld,
                    anyModalOpen,
                    frame,
                    blockedThroughFrame));
        }

        [Test]
        public void InventoryGridPresentation_UsesFixedBackpackAndStashCapacities()
        {
            Assert.AreEqual(6, InventoryPanelController.BackpackColumns);
            Assert.AreEqual(4, InventoryPanelController.BackpackRows);
            Assert.AreEqual(24, InventoryPanelController.BackpackSlotCount);
            Assert.AreEqual(48, InventoryPanelController.StashSlotCount);

            Button item = InventoryPanelController.CreateItemSlot(
                ItemKind.Potion, 3, () => { }, "test-slot");
            Assert.IsTrue(item.ClassListContains("item-grid-slot"));
            Assert.IsTrue(item.Q<VisualElement>(className: "potion-icon") != null);
            Assert.AreEqual("3", item.Q<Label>(className: "inventory-slot-count").text);

            VisualElement empty = InventoryPanelController.CreateEmptySlot("test-empty");
            Assert.IsTrue(empty.ClassListContains("item-grid-slot"));
            Assert.IsTrue(empty.ClassListContains("inventory-empty-slot"));
        }

        [Test]
        public void BackpackPlacement_UsesTouchReadableCellBounds()
        {
            // 44px 은 논리 높이가 **540** 이던 시절의 터치 최소치다. 캔버스가 360 으로
            // 줄었으므로 같은 물리 크기는 44 × 360/540 ≈ 29px 이다. 셀은 36px 이라
            // 터치 영역은 줄어든 게 아니라 오히려 넓어졌다 — 상수를 캔버스에서 파생시켜야
            // 다음에 캔버스가 움직여도 이 테스트가 거짓말을 하지 않는다.
            const float LegacyCanvasMinorAxis = 540f;
            const float LegacyTouchMinimum = 44f;
            float touchMinimum =
                LegacyTouchMinimum * UiPanelScale.DesignMinorAxis / LegacyCanvasMinorAxis;

            var cell = new VisualElement();
            InventoryPanelController.PlaceBackpackElement(cell, 0, 0, 1, 1);
            Assert.GreaterOrEqual(cell.style.width.value.value, touchMinimum);
            Assert.GreaterOrEqual(cell.style.height.value.value, touchMinimum);

            var largeItem = new VisualElement();
            InventoryPanelController.PlaceBackpackElement(largeItem, 1, 1, 2, 2);
            Assert.AreEqual(
                InventoryPanelController.BackpackCellPitch * 2 -
                InventoryPanelController.BackpackCellInset * 2,
                largeItem.style.width.value.value);
        }

        /// <summary>
        /// 배율은 코드가 소유한다(<see cref="UiPanelScale"/>). 에셋은 모드만 정하고
        /// <c>m_Scale</c>은 파일에 1로 남아 있어야 한다 — <c>ResponsiveUiLayout</c>이
        /// 에디터에서 나갈 때 그 값으로 되돌려 유령 diff 를 막기 때문이다.
        /// 둘이 어긋나면 에셋에 diff 가 쌓이거나 배율이 눌러앉는다.
        /// </summary>
        [Test]
        public void PanelSettings_LetCodeOwnTheScale()
        {
            const string path = "Assets/_Project/UI/PrototypePanelSettings.asset";
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            Assert.IsNotNull(settings, $"PanelSettings missing: {path}");

            Assert.AreEqual(
                PanelScaleMode.ConstantPixelSize,
                settings.scaleMode,
                "배율을 코드가 정하려면 ConstantPixelSize 여야 한다.");
            Assert.AreEqual(
                ResponsiveUiLayout.SerializedScale,
                settings.scale,
                "에셋의 m_Scale 은 ResponsiveUiLayout 이 되돌리는 값과 같아야 한다.");
        }

        [Test]
        public void BuildSettings_UseSeparatedFrontEndSceneOrder()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.GreaterOrEqual(scenes.Length, 3);
            Assert.AreEqual("Assets/_Project/Scenes/MainMenu.unity", scenes[0].path);
            Assert.AreEqual("Assets/_Project/Scenes/Hub.unity", scenes[1].path);
            Assert.AreEqual("Assets/_Project/Scenes/IsoPrototype.unity", scenes[2].path);
            Assert.IsTrue(scenes[0].enabled && scenes[1].enabled && scenes[2].enabled);

            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[0].path));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[1].path));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[2].path));
        }

        [Test]
        public void PrototypeScene_WiresBothResponsiveHudLayouts()
        {
            const string scenePath = "Assets/_Project/Scenes/IsoPrototype.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedByTest = !scene.IsValid() || !scene.isLoaded;
            if (openedByTest)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                PrototypeHudController controller = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    controller = root.GetComponentInChildren<PrototypeHudController>(true);
                    if (controller != null)
                        break;
                }

                Assert.IsNotNull(controller, "IsoPrototype scene is missing PrototypeHudController.");
                Assert.IsNotNull(controller.mobileHudAsset, "Mobile HUD wrapper is not wired.");
                Assert.IsNotNull(controller.desktopHudAsset, "Desktop HUD wrapper is not wired.");
                Assert.AreEqual(
                    "Assets/_Project/UI/PrototypeHUD.Mobile.uxml",
                    AssetDatabase.GetAssetPath(controller.mobileHudAsset));
                Assert.AreEqual(
                    "Assets/_Project/UI/PrototypeHUD.Desktop.uxml",
                    AssetDatabase.GetAssetPath(controller.desktopHudAsset));
            }
            finally
            {
                if (openedByTest)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
