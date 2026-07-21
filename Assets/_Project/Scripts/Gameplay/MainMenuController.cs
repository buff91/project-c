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
        private Label _stashLabel;
        private Label _feedbackLabel;
        private VisualElement _heroList;
        private VisualElement _shopList;
        private MetaSaveData _meta;
        private readonly Dictionary<string, Button> _heroCards = new Dictionary<string, Button>();

        private void OnEnable()
        {
            _meta = MetaStore.LoadOrNew();

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _startButton = root.Q<Button>("start-button");
            _continueButton = root.Q<Button>("continue-button");
            _stashLabel = root.Q<Label>("stash-label");
            _feedbackLabel = root.Q<Label>("menu-feedback");
            _heroList = root.Q<VisualElement>("hero-list");
            _shopList = root.Q<VisualElement>("shop-list");
            if (_startButton != null) _startButton.clicked += StartGame;
            if (_continueButton != null)
            {
                _continueButton.clicked += ContinueGame;
                _continueButton.EnableInClassList("is-available", RunSaveStore.HasSave);
            }

            BuildHeroCards(_heroList);
            BuildShop(_shopList);
            UpdateStashLabel();
        }

        /// <summary>상점: 구매하면 창고에 적립 → 새 판 반입. 전리품은 팔지 않는다(파밍 전용).</summary>
        private void BuildShop(VisualElement shopList)
        {
            if (shopList == null) return;
            shopList.Clear();

            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                int price = ItemCatalog.ShopPrice(kind);
                if (price <= 0) continue;

                ItemKind captured = kind;
                var button = new Button(() => BuyItem(captured))
                {
                    name = $"shop-{kind}",
                    text = $"{ItemCatalog.DisplayName(kind)} {price}G"
                };
                button.AddToClassList("shop-item");
                shopList.Add(button);
            }
        }

        private void BuyItem(ItemKind kind)
        {
            int price = ItemCatalog.ShopPrice(kind);
            if (!_meta.TrySpend(price))
            {
                ShowFeedback($"골드가 부족하다 ({_meta.gold}G / {price}G)");
                return;
            }

            _meta.AddCount(kind, 1);
            MetaStore.Save(_meta);
            ShowFeedback($"{ItemCatalog.DisplayName(kind)} 구매 — 창고에 보관됨");
            UpdateStashLabel();
        }

        private void ShowFeedback(string message)
        {
            if (_feedbackLabel != null) _feedbackLabel.text = message;
        }

        /// <summary>창고(메타) 현황: 골드 + 보관 소모품 수. 생환해야만 쌓인다.</summary>
        private void UpdateStashLabel()
        {
            if (_stashLabel == null) return;
            int items = 0;
            foreach (ItemKind kind in ItemCatalog.AllKinds)
                items += _meta.GetCount(kind);
            _stashLabel.text = _meta.gold > 0 || items > 0
                ? $"창고: {_meta.gold}G · 보관 물품 {items}개 (새 판에 반입)"
                : "창고가 비어 있다 — 생환해야 전리품이 쌓인다";
        }

        /// <summary>HeroRoster 를 그대로 카드로 펼친다 — 영웅 추가 시 UXML 수정 불필요.</summary>
        private void BuildHeroCards(VisualElement heroList)
        {
            if (heroList == null) return;
            heroList.Clear();
            _heroCards.Clear();

            foreach (HeroArchetype hero in HeroRoster.All)
            {
                bool unlocked = hero.UnlockCost <= 0 || _meta.IsHeroUnlocked(hero.Id);
                var card = new Button { name = $"hero-{hero.Id}" };
                card.AddToClassList("hero-card");
                card.EnableInClassList("locked", !unlocked);
                card.Add(new Label(hero.DisplayName) { name = "name" });
                card.Q<Label>("name").AddToClassList("hero-card-name");
                var desc = new Label(hero.Description);
                desc.AddToClassList("hero-card-desc");
                card.Add(desc);
                var stats = new Label($"HP {hero.MaxHp} · 근접 {hero.Attack} · 원거리 {hero.RangedDamage}");
                stats.AddToClassList("hero-card-stats");
                card.Add(stats);
                if (!unlocked)
                {
                    var unlock = new Label($"해금 {hero.UnlockCost}G");
                    unlock.AddToClassList("hero-card-unlock");
                    card.Add(unlock);
                }

                string heroId = hero.Id;
                card.clicked += () => OnHeroCardClicked(heroId);
                heroList.Add(card);
                _heroCards.Add(heroId, card);
            }

            // 저장된 선택이 잠긴 영웅이면 기본 영웅으로 되돌린다.
            string selected = HeroSelection.SelectedId ?? HeroRoster.All[0].Id;
            HeroArchetype selectedHero = HeroRoster.ById(selected);
            if (selectedHero.UnlockCost > 0 && !_meta.IsHeroUnlocked(selectedHero.Id))
                selected = HeroRoster.All[0].Id;
            SelectHero(selected);
        }

        private void OnHeroCardClicked(string heroId)
        {
            HeroArchetype hero = HeroRoster.ById(heroId);
            bool unlocked = hero.UnlockCost <= 0 || _meta.IsHeroUnlocked(hero.Id);
            if (unlocked)
            {
                SelectHero(heroId);
                return;
            }

            if (!_meta.TrySpend(hero.UnlockCost))
            {
                ShowFeedback($"골드가 부족하다 ({_meta.gold}G / {hero.UnlockCost}G) — 생환해서 벌어오자");
                return;
            }

            _meta.UnlockHero(hero.Id);
            MetaStore.Save(_meta);
            ShowFeedback($"{hero.DisplayName} 해금!");
            BuildHeroCards(_heroList); // 잠금 표시 갱신
            SelectHero(hero.Id);
            UpdateStashLabel();
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
