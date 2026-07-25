using System.Collections.Generic;
using System.Text;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectC.Gameplay
{
    public partial class HubHudController : MonoBehaviour
    {

        // ── 상점 ─────────────────────────────────────────────

        private void BuildShop()
        {
            if (_shopGrid == null) return;
            _shopGrid.Clear();
            _shopSlots.Clear();
            _shopCounts.Clear();

            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                int price = ItemCatalog.ShopPrice(kind);
                if (price <= 0) continue;

                ItemKind captured = kind;
                var slot = new Button(() => SelectShopItem(captured)) { name = $"shop-{kind}" };
                slot.AddToClassList("inventory-slot");

                var icon = new VisualElement();
                icon.AddToClassList("resource-icon");
                icon.AddToClassList(InventoryPanelController.IconClass(kind));
                slot.Add(icon);

                var priceLabel = new Label(ItemCatalog.FormatGold(price));
                priceLabel.AddToClassList("slot-price");
                slot.Add(priceLabel);

                var owned = new Label("x0");
                owned.AddToClassList("inventory-slot-count");
                slot.Add(owned);

                _shopGrid.Add(slot);
                _shopSlots.Add(kind, slot);
                _shopCounts.Add(kind, owned);
            }
        }

        private void RefreshShop()
        {
            _meta = MetaStore.LoadOrNew();
            if (_shopGold != null) _shopGold.text = ItemCatalog.FormatGold(_meta.gold);
            foreach (var pair in _shopCounts)
                pair.Value.text = $"보유 {_meta.GetCount(pair.Key)}";
            UpdateGoldLabel();
        }

        private void SelectShopItem(ItemKind kind)
        {
            _shopSelected = kind;
            foreach (var pair in _shopSlots)
                pair.Value.EnableInClassList("selected", pair.Key == kind);

            int price = ItemCatalog.ShopPrice(kind);
            if (_shopName != null)
                _shopName.text =
                    $"{ItemCatalog.DisplayName(kind)} — {ItemCatalog.FormatGold(price)}";
            if (_shopDesc != null) _shopDesc.text = ItemCatalog.Description(kind);
            if (_shopBuy != null)
            {
                _shopBuy.SetEnabled(_meta.gold >= price);
                _shopBuy.text = $"구매 ({ItemCatalog.FormatGold(price)})";
            }
            if (_shopFeedback != null) _shopFeedback.text = "";
        }

        private void BuySelected()
        {
            int price = ItemCatalog.ShopPrice(_shopSelected);
            if (!_meta.TrySpend(price))
            {
                if (_shopFeedback != null)
                    _shopFeedback.text =
                        $"소지금이 부족하다 ({ItemCatalog.FormatGold(_meta.gold)} / " +
                        $"{ItemCatalog.FormatGold(price)})";
                return;
            }
            _meta.AddCount(_shopSelected, 1);
            MetaStore.Save(_meta);
            if (_shopFeedback != null)
                _shopFeedback.text = $"{ItemCatalog.DisplayName(_shopSelected)} 구매 완료";
            RefreshShop();
            SelectShopItem(_shopSelected);
            UpdateGoldLabel();
        }

        // ── 대장간 ───────────────────────────────────────────

        private void OpenSmith()
        {
            _meta = MetaStore.LoadOrNew();
            RefreshSmith();
            _smithModal?.BringToFront();
            _smithModal?.AddToClassList("is-open");
        }

        private void RefreshSmith()
        {
            if (_smithGold != null) _smithGold.text = ItemCatalog.FormatGold(_meta.gold);
            if (_smithFeedback != null) _smithFeedback.text = "";
            BuildSmithRows();
            UpdateGoldLabel();
        }

        private void BuildSmithRows()
        {
            if (_smithList == null) return;
            _smithList.Clear();

            foreach (EquipmentDefinition equipment in EquipmentCatalog.All)
            {
                bool owned = _meta.OwnsEquipment(equipment);
                bool equipped = ForgeRules.IsEquipped(_meta, equipment);
                string slotLabel = equipment.Slot == EquipmentSlot.Weapon ? "무기" : "보조";

                var row = new VisualElement { name = $"smith-row-{equipment.Id}" };
                row.AddToClassList("hub-smith-row");

                var info = new VisualElement();
                info.AddToClassList("hub-smith-info");
                var title = new Label(
                    $"{equipment.DisplayName}  ·  {slotLabel}{(equipped ? "  ·  장착 중" : "")}");
                title.AddToClassList("hub-row-title");
                var desc = new Label(equipment.Description);
                desc.AddToClassList("hub-row-desc");
                info.Add(title);
                info.Add(desc);
                row.Add(info);

                string equipmentId = equipment.Id;
                var action = new Button(() => ForgeAction(equipmentId))
                {
                    name = $"smith-buy-{equipment.Id}",
                    text = owned
                        ? equipped ? "해제" : "장착"
                        : $"제작 ({ItemCatalog.FormatGold(equipment.CraftCost)})"
                };
                action.AddToClassList("settings-done");
                action.AddToClassList("hub-smith-buy");
                action.SetEnabled(owned || _meta.gold >= equipment.CraftCost);
                row.Add(action);

                _smithList.Add(row);
            }
        }

        /// <summary>보유 전이면 제작, 보유 후면 장착/해제 토글 — 버튼 하나로 장비를 관리한다.</summary>
        private void ForgeAction(string equipmentId)
        {
            EquipmentDefinition equipment = EquipmentCatalog.ById(equipmentId);
            string label = equipment != null ? equipment.DisplayName : equipmentId;

            if (equipment != null && _meta.OwnsEquipment(equipment))
            {
                if (ForgeRules.TryToggleEquip(_meta, equipmentId))
                {
                    MetaStore.Save(_meta);
                    if (_smithFeedback != null)
                        _smithFeedback.text = ForgeRules.IsEquipped(_meta, equipment)
                            ? $"{label} 장착"
                            : $"{label} 해제";
                }
            }
            else
            {
                switch (ForgeRules.TryCraft(_meta, equipmentId))
                {
                    case ForgeResult.Crafted:
                        MetaStore.Save(_meta);
                        if (_smithFeedback != null) _smithFeedback.text = $"{label} 제작 완료 — 장착했다";
                        break;
                    case ForgeResult.InsufficientGold:
                        if (_smithFeedback != null)
                            _smithFeedback.text = "소지금이 부족하다 — 생환해서 벌어오자";
                        break;
                    case ForgeResult.AlreadyOwned:
                        if (_smithFeedback != null) _smithFeedback.text = $"{label}은(는) 이미 가지고 있다";
                        break;
                }
            }

            BuildSmithRows();
            if (_smithGold != null) _smithGold.text = ItemCatalog.FormatGold(_meta.gold);
            UpdateGoldLabel();
        }

        // ── 의뢰 게시판 ──────────────────────────────────────

        private void OpenBounty()
        {
            _meta = MetaStore.LoadOrNew();
            if (!BountyRules.HasActiveBounties(_meta))
            {
                BountyRules.AssignOffers(_meta, System.Environment.TickCount);
                MetaStore.Save(_meta);
            }
            RefreshBounty();
            _bountyModal?.BringToFront();
            _bountyModal?.AddToClassList("is-open");
        }

        private void RefreshBounty()
        {
            if (_bountyGold != null) _bountyGold.text = ItemCatalog.FormatGold(_meta.gold);
            if (_bountyList == null) return;
            _bountyList.Clear();

            List<BountyDefinition> active = BountyRules.ActiveBounties(_meta);
            if (active.Count == 0)
            {
                var empty = new Label("걸린 의뢰가 없다. 다음 원정에서 새 계약이 걸린다.");
                empty.AddToClassList("hub-bounty-empty");
                _bountyList.Add(empty);
            }

            foreach (BountyDefinition bounty in active)
            {
                var row = new VisualElement { name = $"bounty-row-{bounty.Id}" };
                row.AddToClassList("hub-bounty-row");

                var title = new Label(bounty.DisplayName);
                title.AddToClassList("hub-row-title");
                var desc = new Label(bounty.Description);
                desc.AddToClassList("hub-row-desc");
                var reward = new Label(
                    $"보상 {ItemCatalog.FormatGold(bounty.RewardGold)} · 생환 시 지급");
                reward.AddToClassList("hub-bounty-reward");

                row.Add(title);
                row.Add(desc);
                row.Add(reward);
                _bountyList.Add(row);
            }

            UpdateGoldLabel();
        }

        // ── 기록실 ───────────────────────────────────────────

        /// <summary>
        /// 해금 조건과 진행값을 보여준다. <b>이 화면이 안내를 맡는 이유</b>는 해금 안내를
        /// 의뢰로 줄 수 없기 때문이다 — 의뢰 게시판은 잠기는 시설이라 거기서 안내하면 순환이 된다.
        /// 그래서 기록실은 항상 열려 있다.
        /// </summary>
        private void OpenCodex()
        {
            CloseModals();
            RefreshCodex();
            _codexModal?.BringToFront();
            _codexModal?.AddToClassList("is-open");
        }

        private void RefreshCodex()
        {
            if (_codexList == null) return;
            _codexList.Clear();

            List<ItemKind> unlocked = _meta.UnlockedItemKinds();
            string[] rescued = _meta.rescuedNpcs ?? new string[0];
            int found = ItemUnlockRules.UnlockedCount(unlocked) +
                        ShelterNpcRoster.RescuedCount(rescued);
            int total = ItemUnlockRules.TotalCount + ShelterNpcRoster.TotalCount;
            if (_codexCount != null) _codexCount.text = $"기록 {found}/{total}";

            foreach (ItemUnlockCondition condition in ItemUnlockRules.Conditions)
            {
                bool open = _meta.IsItemUnlocked(condition.Kind);
                var row = new VisualElement { name = $"codex-row-{condition.Kind}" };
                row.AddToClassList("hub-bounty-row");
                row.EnableInClassList("is-locked", !open);

                // 해금은 이름을 드러내고, 미해금은 가린다 — 무엇이 남았는지가 궁금함으로 남게.
                var title = new Label(open ? ItemCatalog.DisplayName(condition.Kind) : "???");
                title.AddToClassList("hub-row-title");
                row.Add(title);

                var desc = new Label(
                    open ? ItemCatalog.Description(condition.Kind) : condition.Requirement);
                desc.AddToClassList("hub-row-desc");
                row.Add(desc);

                // 최고 기록을 보여준다 — 조건이 한 판 기준이라 지난 판 값을 쓰면
                // 나쁜 판 뒤에 0 으로 돌아가 안내가 쓸모없어진다.
                var status = new Label(open
                    ? "해금됨 · 원정에서 등장한다"
                    : $"최고 기록 {_meta.BestUnlockProgress(condition.Kind)}/{condition.Target}");
                status.AddToClassList("hub-bounty-reward");
                row.Add(status);

                _codexList.Add(row);
            }

            // 동료는 조건이 아니라 장소로 열린다 — 어느 층에 갇혀 있는지를 알려 준다.
            // 그래서 "얼마나 남았나"가 아니라 "어디로 가야 하나"가 안내다.
            foreach (ShelterNpcDefinition npc in ShelterNpcRoster.All)
            {
                bool joined = _meta.IsNpcRescued(npc.Id);
                var row = new VisualElement { name = $"codex-npc-{npc.Id}" };
                row.AddToClassList("hub-bounty-row");
                row.EnableInClassList("is-locked", !joined);

                var title = new Label(joined ? npc.DisplayName : "갇힌 동료");
                title.AddToClassList("hub-row-title");
                row.Add(title);

                var desc = new Label(joined
                    ? npc.RescueDetail
                    : $"{npc.ProgressIndex + 1}번째 층의 잠긴 방에 갇혀 있다");
                desc.AddToClassList("hub-row-desc");
                row.Add(desc);

                var status = new Label(joined
                    ? "합류함 · 시설이 열렸다"
                    : $"구출하면 {FacilityLabel(npc.Facility)}이 열린다");
                status.AddToClassList("hub-bounty-reward");
                row.Add(status);

                _codexList.Add(row);
            }
        }

        private static string FacilityLabel(ShelterFacility facility) =>
            facility == ShelterFacility.Forge ? "대장간" : "의뢰 게시판";

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
                                  (unlocked
                                      ? ""
                                      : $" · 해금 {ItemCatalog.FormatGold(hero.UnlockCost)}");
            if (_heroAction != null)
            {
                _heroAction.text = !unlocked
                    ? $"해금 ({ItemCatalog.FormatGold(hero.UnlockCost)})"
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
                        _statusLabel.text =
                            $"소지금이 부족하다 ({ItemCatalog.FormatGold(_meta.gold)} / " +
                            $"{ItemCatalog.FormatGold(hero.UnlockCost)}) — 생환해서 벌어오자";
                    CloseModals();
                    return;
                }
                _meta.UnlockHero(hero.Id);
                MetaStore.Save(_meta);
                if (_statusLabel != null) _statusLabel.text = $"{hero.DisplayName} 해금!";
            }

            HeroSelection.SelectedId = hero.Id;
            int returned = ExpeditionLoadoutRules.Reconcile(_meta, hero);
            MetaStore.Save(_meta);
            demo?.RefreshHubHeroLocks();
            if (_statusLabel != null)
                _statusLabel.text = returned > 0
                    ? $"{hero.DisplayName} 합류 · 공간 조정으로 {returned}개 창고 복귀"
                    : $"{hero.DisplayName} 합류 · 이전 영웅은 대기 위치로 복귀";
            UpdateGoldLabel();
            OpenHero(hero.Id); // 버튼 상태 갱신
        }
    }
}
