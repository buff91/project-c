using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public partial class IsoPrototypeDemo
    {

        // ── 허브 캠프 ─────────────────────────────────────────

        /// <summary>
        /// 캠프 프롭 생성: 모닥불·포탈·상인·창고·기록실 + 구출로 열리는 시설(대장간·의뢰 게시판).
        /// 탭 상호작용 좌표도 등록한다. 영웅 프롭·선택 모달은 단일 원정자로 가면서 없앴다.
        /// </summary>
        private void CreateHubProps()
        {
            _hubInteractables.Clear();
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

            ApplySurvivorStats();
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
                lightTile.transform.position = VisualPosition(pos);
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
        /// 캠프의 플레이어에게 원정자 기본값을 입힌다. 예전에는 영웅 선택을 반영하는
        /// 자리였는데, 고를 것이 없어졌으므로 값을 <see cref="SurvivorProfile"/>에서 바로 읽는다.
        /// </summary>
        public void ApplySurvivorStats()
        {
            if (!hubMode) return;

            playerMaxHp = SurvivorProfile.MaxHp;
            playerAttack = SurvivorProfile.Attack;
            rangedAttackDamage = SurvivorProfile.RangedDamage;

            if (_playerRenderer != null)
            {
                Sprite sprite = visualCatalog != null ? visualCatalog.SurvivorSprite : null;
                _playerRenderer.sprite = sprite != null
                    ? sprite
                    : ActorSprites.GetCharacterSprite(false);
                _playerRenderer.color = Color.white;
            }

            if (_playerState != null)
            {
                _playerState = new CombatantState(
                    "Player", _playerState.Position, SurvivorProfile.MaxHp, SurvivorProfile.Attack);
                UpdateHealthBar(_playerHpFill, _playerState);
                PlayerHpChanged?.Invoke();
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
