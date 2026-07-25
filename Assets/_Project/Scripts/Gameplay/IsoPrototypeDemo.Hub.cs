using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public partial class IsoPrototypeDemo
    {

        // ── 허브 캠프 ─────────────────────────────────────────

        /// <summary>캠프 프롭 생성: 상인/영웅 3명/창고/포탈/모닥불. 탭 상호작용 좌표도 등록한다.</summary>
        private void CreateHubProps()
        {
            _hubInteractables.Clear();
            _hubHeroProps.Clear();
            _hubHeroPositions.Clear();
            _hubPropPositions.Clear();
            _hubLightPositions.Clear();

            Sprite campfire = visualCatalog != null ? visualCatalog.hubCampfire : null;
            Sprite portal = visualCatalog != null ? visualCatalog.hubPortal : null;
            Sprite merchantSprite = visualCatalog != null ? visualCatalog.merchant : null;
            Sprite stash = visualCatalog != null ? visualCatalog.hubStash : null;

            CreateHubLightPatch("campfire", HubLayout.Campfire, 2);
            CreateHubLightPatch("portal", HubLayout.Portal, 1);

            CreateHubProp("Campfire", campfire != null ? campfire : ActorSprites.GetHubPropSprite("campfire"), HubLayout.Campfire);
            CreateHubProp("Portal", portal != null ? portal : ActorSprites.GetHubPropSprite("portal"), HubLayout.Portal);

            CreateHubProp(
                "Merchant",
                merchantSprite != null ? merchantSprite : ActorSprites.GetCharacterSprite(true),
                HubLayout.Merchant);
            _hubInteractables[HubLayout.Merchant] = "merchant";

            CreateHubProp("Stash", stash != null ? stash : ActorSprites.GetHubPropSprite("stash"), HubLayout.Stash);
            _hubInteractables[HubLayout.Stash] = "stash";

            // 구출로 열리는 시설은 동료가 합류한 뒤에만 존재한다 — 프롭도 상호작용도 없다.
            // 빈 모달을 보여주는 것보다 "아직 없다"가 정직하고, 구출이 사건이 된다.
            MetaSaveData shelterMeta = MetaStore.LoadOrNew();

            if (shelterMeta.IsFacilityOpen(ShelterFacility.Forge))
            {
                CreateHubProp("Smith", ActorSprites.GetHubPropSprite("smith"), HubLayout.Smith);
                _hubInteractables[HubLayout.Smith] = "smith";
            }

            if (shelterMeta.IsFacilityOpen(ShelterFacility.BountyBoard))
            {
                CreateHubProp(
                    "BountyBoard", ActorSprites.GetHubPropSprite("bounty"), HubLayout.BountyBoard);
                _hubInteractables[HubLayout.BountyBoard] = "bounty";
            }

            // 기록실은 항상 열려 있다 — 해금 조건을 배우는 유일한 창구이고, 그 안내를
            // 의뢰로 줄 수 없기 때문이다(의뢰 게시판은 잠기는 시설이라 순환이 된다).
            CreateHubProp("Codex", ActorSprites.GetHubPropSprite("codex"), HubLayout.Codex);
            _hubInteractables[HubLayout.Codex] = "codex";

            for (int i = 0; i < HeroRoster.All.Count && i < HubLayout.HeroPositions.Count; i++)
            {
                HeroArchetype hero = HeroRoster.All[i];
                Sprite heroSprite = visualCatalog != null ? visualCatalog.HeroFor(hero.Id) : null;
                var prop = CreateHubProp(
                    $"Hero {hero.Id}",
                    heroSprite != null ? heroSprite : ActorSprites.GetCharacterSprite(false),
                    HubLayout.HeroPositions[i]);
                _hubHeroProps[hero.Id] = prop;
                _hubHeroPositions[hero.Id] = HubLayout.HeroPositions[i];
            }

            RefreshHubHeroLocks();
        }

        private void CreateHubLightPatch(string kind, GridPos origin, int radius)
        {
            foreach (KeyValuePair<GridPos, TileData> pair in _grid.Map.All())
            {
                GridPos pos = pair.Key;
                if (!pair.Value.IsWalkable || pos.elevation != origin.elevation)
                    continue;

                int distance = pos.ManhattanTo(origin);
                if (distance > radius) continue;

                int strength = distance == 0 ? 3 : distance == 1 ? 2 : 1;
                var lightTile = new GameObject($"{kind} Light {pos.x},{pos.y}");
                lightTile.transform.SetParent(_visualRoot, false);
                lightTile.transform.position = _grid.GridToWorld(pos);
                var renderer = lightTile.AddComponent<SpriteRenderer>();
                renderer.sprite = GetHubLightTileSprite(kind, strength);
                renderer.sortingOrder = _grid.iso.SortingOrder(pos, -1);
                _hubLightPositions[renderer] = pos;
            }
        }

        private SpriteRenderer CreateHubProp(string objectName, Sprite sprite, GridPos pos)
        {
            CreateStandingSprite(objectName, sprite, pos, out SpriteRenderer renderer);
            _hubPropPositions[renderer] = pos;
            return renderer;
        }

        /// <summary>
        /// 선택 영웅은 플레이어로 표시하고 대기 위치에서는 숨긴다.
        /// 나머지 영웅은 자기 위치에 복귀하며, 잠긴 영웅만 회색으로 표시한다.
        /// </summary>
        public void RefreshHubHeroLocks()
        {
            if (!hubMode) return;
            MetaSaveData meta = MetaStore.LoadOrNew();
            HeroArchetype selectedHero = HeroRoster.ById(HeroSelection.SelectedId);

            _hero = selectedHero;
            playerMaxHp = selectedHero.MaxHp;
            playerAttack = selectedHero.Attack;
            rangedAttackDamage = selectedHero.RangedDamage;

            if (_playerRenderer != null)
            {
                Sprite selectedSprite = visualCatalog != null
                    ? visualCatalog.HeroFor(selectedHero.Id)
                    : null;
                _playerRenderer.sprite = selectedSprite != null
                    ? selectedSprite
                    : ActorSprites.GetCharacterSprite(false);
                _playerRenderer.color = Color.white;
            }

            if (_playerState != null)
            {
                _playerState = new CombatantState(
                    "Player", _playerState.Position, selectedHero.MaxHp, selectedHero.Attack);
                UpdateHealthBar(_playerHpFill, _playerState);
                PlayerHpChanged?.Invoke();
            }

            foreach (KeyValuePair<string, SpriteRenderer> pair in _hubHeroProps)
            {
                HeroArchetype hero = HeroRoster.ById(pair.Key);
                bool unlocked = hero.UnlockCost <= 0 || meta.IsHeroUnlocked(hero.Id);
                bool showAtRosterPosition = HubLayout.ShouldShowHeroAtRosterPosition(
                    hero.Id, selectedHero.Id);

                pair.Value.gameObject.SetActive(showAtRosterPosition);
                pair.Value.color = !unlocked
                    ? (Color)new Color32(96, 100, 104, 255)
                    : Color.white;

                if (!_hubHeroPositions.TryGetValue(hero.Id, out GridPos position))
                    continue;
                if (showAtRosterPosition)
                    _hubInteractables[position] = $"hero:{hero.Id}";
                else
                    _hubInteractables.Remove(position);
            }
        }

        /// <summary>NPC/오브젝트 옆까지 걸어간 뒤 상호작용 이벤트를 쏜다. (허브 전용)</summary>
        private IEnumerator ApproachAndInteract(IReadOnlyList<GridPos> path, GridPos target, string id)
        {
            yield return MovePlayerPath(path);
            if (_playerState.IsAlive && IsPlayerAdjacentTo(target))
                HubInteractionRequested?.Invoke(id);
        }
    }
}
