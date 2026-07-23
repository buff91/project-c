using ProjectC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 타이틀 씬의 얇은 라우터. 이후 프롤로그/세계관 씬을 추가할 때 StartNewGame의
    /// 목적지만 바꾸면 허브와 던전 씬을 건드리지 않고 흐름을 확장할 수 있다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        private Button _startButton;
        private Button _continueButton;
        private Button _quitButton;
        private ResponsiveUiLayout _responsiveLayout;
        private DisplaySettingsPanelController _displaySettings;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _responsiveLayout = new ResponsiveUiLayout(
                root, root.Q<VisualElement>("main-menu-root"));
            _startButton = root.Q<Button>("main-start-button");
            _continueButton = root.Q<Button>("main-continue-button");
            _quitButton = root.Q<Button>("main-quit-button");

            if (_startButton != null) _startButton.clicked += StartNewGame;
            if (_continueButton != null)
            {
                _continueButton.clicked += ContinueRun;
                _continueButton.SetEnabled(RunSaveStore.HasSave);
            }
            if (_quitButton != null) _quitButton.clicked += QuitGame;

            _displaySettings = new DisplaySettingsPanelController(
                root, null, "main-settings-button");
        }

        private void OnDisable()
        {
            if (_startButton != null) _startButton.clicked -= StartNewGame;
            if (_continueButton != null) _continueButton.clicked -= ContinueRun;
            if (_quitButton != null) _quitButton.clicked -= QuitGame;
            _startButton = null;
            _continueButton = null;
            _quitButton = null;

            _responsiveLayout?.Dispose();
            _responsiveLayout = null;
            _displaySettings?.Dispose();
            _displaySettings = null;
        }

        private static void StartNewGame()
        {
            RunSaveStore.ContinueRequested = false;
            DungeonSelection.SelectedId = DungeonCatalog.DefaultId;
            SceneManager.LoadScene(FrontEndFlow.HubScene);
        }

        private static void ContinueRun()
        {
            if (!RunSaveStore.HasSave) return;
            RunSaveStore.ContinueRequested = true;
            SceneManager.LoadScene(FrontEndFlow.DungeonScene);
        }

        private static void QuitGame() => Application.Quit();
    }
}
