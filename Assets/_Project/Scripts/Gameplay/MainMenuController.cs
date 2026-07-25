using ProjectC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 타이틀 씬의 얇은 라우터. `게임 시작`은 언제나 캠프로 가고 `이어하기`는 던전 중간
    /// 저장이 있을 때만 나타난다(<see cref="TitleEntryRouting"/>). 이후 프롤로그/세계관
    /// 씬은 <see cref="TitleEntryRouting.StartScene"/>만 바꿔 삽입한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        private Button _startButton;
        private Button _resumeButton;
        private Button _quitButton;
        private ResponsiveUiLayout _responsiveLayout;
        private DisplaySettingsPanelController _displaySettings;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _responsiveLayout = new ResponsiveUiLayout(
                root, root.Q<VisualElement>("main-menu-root"));
            _startButton = root.Q<Button>("main-start-button");
            _resumeButton = root.Q<Button>("main-continue-button");
            _quitButton = root.Q<Button>("main-quit-button");

            if (_startButton != null) _startButton.clicked += EnterCamp;
            if (_resumeButton != null)
            {
                // 비활성 회색이 아니라 아예 없앤다 — 누를 수 없는 버튼은 노이즈다.
                _resumeButton.style.display = TitleEntryRouting.ShowsResume(RunSaveStore.HasSave)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                _resumeButton.clicked += ResumeRun;
            }
            if (_quitButton != null) _quitButton.clicked += QuitGame;

            _displaySettings = new DisplaySettingsPanelController(
                root, null, "main-settings-button");
        }

        private void OnDisable()
        {
            if (_startButton != null) _startButton.clicked -= EnterCamp;
            if (_resumeButton != null) _resumeButton.clicked -= ResumeRun;
            if (_quitButton != null) _quitButton.clicked -= QuitGame;
            _startButton = null;
            _resumeButton = null;
            _quitButton = null;

            _responsiveLayout?.Dispose();
            _responsiveLayout = null;
            _displaySettings?.Dispose();
            _displaySettings = null;
        }

        /// <summary>첫 실행이든 재접속이든 캠프가 게임의 시작점이다.</summary>
        private static void EnterCamp()
        {
            RunSaveStore.ContinueRequested = false;
            DungeonSelection.SelectedId = DungeonCatalog.DefaultId;
            SceneManager.LoadScene(TitleEntryRouting.StartScene);
        }

        /// <summary>던전 중간 저장을 이어받는다 — 체크포인트가 있을 때만 버튼이 보인다.</summary>
        private static void ResumeRun()
        {
            // 세이브가 사라진 채로 눌렸다면(프로필 전환 등) 캠프로 흘려보낸다.
            if (!RunSaveStore.HasSave)
            {
                EnterCamp();
                return;
            }
            RunSaveStore.ContinueRequested = true;
            SceneManager.LoadScene(TitleEntryRouting.ResumeScene);
        }

        private static void QuitGame() => Application.Quit();
    }
}
