using System.Text;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 허브 캠프 HUD: 골드/이어하기 + 상점·영웅·창고 모달.
    /// 열리는 계기는 데모의 HubInteractionRequested(NPC 옆까지 걸어간 뒤)다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HubHudController : MonoBehaviour
    {
        public IsoPrototypeDemo demo;

        private MetaSaveData _meta;
        private Label _goldLabel;
        private Label _statusLabel;
        private Button _continueButton;
        private VisualElement _shopModal;
        private VisualElement _shopList;
        private Label _shopFeedback;
        private VisualElement _heroModal;
        private Label _heroName;
        private Label _heroDesc;
        private Label _heroStats;
        private Button _heroAction;
        private VisualElement _stashModal;
        private Label _stashDetail;
        private string _heroModalId;
        private IsoTapInput _tapInput;

        private void OnEnable()
        {
            _meta = MetaStore.LoadOrNew();

            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _goldLabel = root.Q<Label>("hub-gold");
            _statusLabel = root.Q<Label>("hub-status");
            _continueButton = root.Q<Button>("hub-continue");
            _shopModal = root.Q<VisualElement>("hub-shop-modal");
            _shopList = root.Q<VisualElement>("hub-shop-list");
            _shopFeedback = root.Q<Label>("hub-shop-feedback");
            _heroModal = root.Q<VisualElement>("hub-hero-modal");
            _heroName = root.Q<Label>("hub-hero-name");
            _heroDesc = root.Q<Label>("hub-hero-desc");
            _heroStats = root.Q<Label>("hub-hero-stats");
            _heroAction = root.Q<Button>("hub-hero-action");
            _stashModal = root.Q<VisualElement>("hub-stash-modal");
            _stashDetail = root.Q<Label>("hub-stash-detail");

            Bind(root.Q<Button>("hub-shop-close"), CloseModals);
            Bind(root.Q<Button>("hub-hero-close"), CloseModals);
            Bind(root.Q<Button>("hub-stash-close"), CloseModals);
            Bind(_heroAction, HandleHeroAction);
            Bind(_continueButton, ContinueRun);

            if (_continueButton != null)
                _continueButton.EnableInClassList("is-available", RunSaveStore.HasSave);

            BuildShop();
            UpdateGoldLabel();

            if (demo != null)
            {
                demo.HubInteractionRequested += HandleHubInteraction;
                demo.InteractionFeedback += HandleFeedback;
                _tapInput = demo.GetComponent<IsoTapInput>();
                if (_tapInput != null) _tapInput.UiBlocker = IsPointerOverHud;
            }
        }

        private void OnDisable()
        {
            if (demo != null)
            {
                demo.HubInteractionRequested -= HandleHubInteraction;
                demo.InteractionFeedback -= HandleFeedback;
            }
            if (_tapInput != null && _tapInput.UiBlocker == IsPointerOverHud)
                _tapInput.UiBlocker = null;
            _tapInput = null;
        }

        private static void Bind(Button button, System.Action onClick)
        {
            if (button != null) button.clicked += () => onClick();
        }

        private bool IsPointerOverHud(Vector2 screenPoint)
        {
            IPanel panel = GetComponent<UIDocument>().rootVisualElement?.panel;
            if (panel == null) return false;
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(
                panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            for (VisualElement element = panel.Pick(panelPoint); element != null; element = element.parent)
            {
                if (element is Button || element is ScrollView) return true;
                if (element.ClassListContains("artifact-panel") ||
                    element.ClassListContains("settings-modal") ||
                    element.ClassListContains("status-chip"))
                    return true;
            }
            return false;
        }

        // ── 상호작용 라우팅 ──────────────────────────────────

        private void HandleHubInteraction(string id)
        {
            CloseModals();
            if (id == "merchant") _shopModal?.AddToClassList("is-open");
            else if (id == "stash") OpenStash();
            else if (id.StartsWith("hero:")) OpenHero(id.Substring(5));
        }

        private void HandleFeedback(string message)
        {
            if (_statusLabel != null) _statusLabel.text = message;
        }

        private void CloseModals()
        {
            _shopModal?.RemoveFromClassList("is-open");
            _heroModal?.RemoveFromClassList("is-open");
            _stashModal?.RemoveFromClassList("is-open");
        }

        // ── 상점 ─────────────────────────────────────────────

        private void BuildShop()
        {
            if (_shopList == null) return;
            _shopList.Clear();
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                int price = ItemCatalog.ShopPrice(kind);
                if (price <= 0) continue;
                ItemKind captured = kind;
                var button = new Button(() => BuyItem(captured))
                {
                    text = $"{ItemCatalog.DisplayName(kind)}\n{price}G"
                };
                button.AddToClassList("hub-shop-item");
                _shopList.Add(button);
            }
        }

        private void BuyItem(ItemKind kind)
        {
            int price = ItemCatalog.ShopPrice(kind);
            if (!_meta.TrySpend(price))
            {
                if (_shopFeedback != null)
                    _shopFeedback.text = $"골드가 부족하다 ({_meta.gold}G / {price}G)";
                return;
            }
            _meta.AddCount(kind, 1);
            MetaStore.Save(_meta);
            if (_shopFeedback != null)
                _shopFeedback.text = $"{ItemCatalog.DisplayName(kind)} 구매 — 창고 보관 (보유 {_meta.GetCount(kind)})";
            UpdateGoldLabel();
        }

        // ── 영웅 ─────────────────────────────────────────────

        private void OpenHero(string heroId)
        {
            _heroModalId = heroId;
            HeroArchetype hero = HeroRoster.ById(heroId);
            bool unlocked = hero.UnlockCost <= 0 || _meta.IsHeroUnlocked(hero.Id);
            bool selected = (HeroSelection.SelectedId ?? HeroRoster.All[0].Id) == hero.Id;

            if (_heroName != null) _heroName.text = hero.DisplayName;
            if (_heroDesc != null) _heroDesc.text = hero.Description;
            if (_heroStats != null)
                _heroStats.text = $"HP {hero.MaxHp} · 근접 {hero.Attack} · 원거리 {hero.RangedDamage}" +
                                  (unlocked ? "" : $" · 해금 {hero.UnlockCost}G");
            if (_heroAction != null)
            {
                _heroAction.text = !unlocked ? $"해금 ({hero.UnlockCost}G)"
                    : selected ? "선택됨"
                    : "선택";
                _heroAction.SetEnabled(!selected || !unlocked);
            }
            _heroModal?.AddToClassList("is-open");
        }

        private void HandleHeroAction()
        {
            if (string.IsNullOrEmpty(_heroModalId)) return;
            HeroArchetype hero = HeroRoster.ById(_heroModalId);
            bool unlocked = hero.UnlockCost <= 0 || _meta.IsHeroUnlocked(hero.Id);

            if (!unlocked)
            {
                if (!_meta.TrySpend(hero.UnlockCost))
                {
                    if (_statusLabel != null)
                        _statusLabel.text = $"골드가 부족하다 ({_meta.gold}G / {hero.UnlockCost}G) — 생환해서 벌어오자";
                    CloseModals();
                    return;
                }
                _meta.UnlockHero(hero.Id);
                MetaStore.Save(_meta);
                if (_statusLabel != null) _statusLabel.text = $"{hero.DisplayName} 해금!";
            }

            HeroSelection.SelectedId = hero.Id;
            demo?.RefreshHubHeroLocks();
            UpdateGoldLabel();
            OpenHero(hero.Id); // 버튼 상태 갱신
        }

        // ── 창고 / 공통 ──────────────────────────────────────

        private void OpenStash()
        {
            if (_stashDetail != null)
            {
                var text = new StringBuilder();
                text.AppendLine($"골드: {_meta.gold}G");
                bool any = false;
                foreach (ItemKind kind in ItemCatalog.AllKinds)
                {
                    int count = _meta.GetCount(kind);
                    if (count <= 0) continue;
                    text.AppendLine($"{ItemCatalog.DisplayName(kind)} ×{count}");
                    any = true;
                }
                if (!any) text.AppendLine("보관 물품 없음 — 생환하거나 상점에서 사두자");
                text.Append("보관품은 새 판 시작 시 전량 반입된다");
                _stashDetail.text = text.ToString();
            }
            _stashModal?.AddToClassList("is-open");
        }

        private void UpdateGoldLabel()
        {
            if (_goldLabel != null) _goldLabel.text = $"{_meta.gold}G";
        }

        private void ContinueRun()
        {
            RunSaveStore.ContinueRequested = true;
            SceneManager.LoadScene("IsoPrototype");
        }
    }
}
