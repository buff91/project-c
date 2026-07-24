using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEditor;
using UnityEngine;
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
            "minimap-view",
            "settings-button",
            "game-menu-button",
            "rotate-left",
            "rotate-right",
            "view-label",
            "depth-label",
            "depth-caption",
            "location-label",
            "floor-label",
            "status-label",
            "vertical-hint-label",
            "vertical-route-discovery",
            "route-discovery-title",
            "route-discovery-detail",
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
            "gameover-overlay"
        };

        private static readonly string[] HubRequiredNames =
        {
            "hub-root",
            "hub-settings-button",
            "hub-menu-button",
            "hub-dungeon-modal",
            "hub-dungeon-catacombs",
            "hub-dungeon-loadout",
            "hub-dungeon-enter",
            "hub-menu-modal",
            "hub-menu-resume",
            "hub-menu-quit",
            "hub-shop-modal",
            "hub-hero-modal",
            "hub-stash-modal",
            "hub-stash-grid",
            "hub-stash-capacity",
            "hub-stash-detail-icon",
            "hub-loadout-grid",
            "hub-loadout-capacity",
            "hub-to-loadout",
            "hub-to-stash"
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
        public void TorchstoneActionIcons_AreCrispTwentyFourPixelSprites()
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
                Assert.AreEqual(24, icon.width, $"{path} width");
                Assert.AreEqual(24, icon.height, $"{path} height");
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
            var cell = new VisualElement();
            InventoryPanelController.PlaceBackpackElement(cell, 0, 0, 1, 1);
            Assert.GreaterOrEqual(cell.style.width.value.value, 44f);
            Assert.GreaterOrEqual(cell.style.height.value.value, 44f);

            var largeItem = new VisualElement();
            InventoryPanelController.PlaceBackpackElement(largeItem, 1, 1, 2, 2);
            Assert.AreEqual(
                InventoryPanelController.BackpackCellPitch * 2 -
                InventoryPanelController.BackpackCellInset * 2,
                largeItem.style.width.value.value);
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
    }
}
