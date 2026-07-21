using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ProjectC.Gameplay
{
    /// <summary>메인 메뉴(로비). UI Toolkit 전용 화면 — 시작하면 프로토타입 씬을 로드한다.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        public string gameSceneName = "IsoPrototype";

        private Button _startButton;
        private Button _continueButton;
        private readonly Dictionary<string, Button> _heroCards = new Dictionary<string, Button>();

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _startButton = root.Q<Button>("start-button");
            _continueButton = root.Q<Button>("continue-button");
            if (_startButton != null) _startButton.clicked += StartGame;
            if (_continueButton != null)
            {
                _continueButton.clicked += ContinueGame;
                _continueButton.EnableInClassList("is-available", RunSaveStore.HasSave);
            }
            BuildHeroCards(root.Q<VisualElement>("hero-list"));
        }

        /// <summary>HeroRoster 를 그대로 카드로 펼친다 — 영웅 추가 시 UXML 수정 불필요.</summary>
        private void BuildHeroCards(VisualElement heroList)
        {
            if (heroList == null) return;
            heroList.Clear();
            _heroCards.Clear();

            foreach (HeroArchetype hero in HeroRoster.All)
            {
                var card = new Button { name = $"hero-{hero.Id}" };
                card.AddToClassList("hero-card");
                card.Add(new Label(hero.DisplayName) { name = "name" });
                card.Q<Label>("name").AddToClassList("hero-card-name");
                var desc = new Label(hero.Description);
                desc.AddToClassList("hero-card-desc");
                card.Add(desc);
                var stats = new Label($"HP {hero.MaxHp} · 근접 {hero.Attack} · 원거리 {hero.RangedDamage}");
                stats.AddToClassList("hero-card-stats");
                card.Add(stats);

                string heroId = hero.Id;
                card.clicked += () => SelectHero(heroId);
                heroList.Add(card);
                _heroCards.Add(heroId, card);
            }

            SelectHero(HeroSelection.SelectedId ?? HeroRoster.All[0].Id);
        }

        private void SelectHero(string heroId)
        {
            HeroSelection.SelectedId = HeroRoster.ById(heroId).Id;
            foreach (KeyValuePair<string, Button> pair in _heroCards)
                pair.Value.EnableInClassList("selected", pair.Key == HeroSelection.SelectedId);
        }

        private void OnDisable()
        {
            if (_startButton != null) _startButton.clicked -= StartGame;
            if (_continueButton != null) _continueButton.clicked -= ContinueGame;
            _startButton = null;
            _continueButton = null;
        }

        private void StartGame()
        {
            // 새로 시작은 기존 체크포인트를 버린다.
            RunSaveStore.Clear();
            RunSaveStore.ContinueRequested = false;
            SceneManager.LoadScene(gameSceneName);
        }

        private void ContinueGame()
        {
            RunSaveStore.ContinueRequested = true;
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
