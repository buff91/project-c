using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public partial class IsoPrototypeDemo
    {
        private readonly HubWorldPresenter _hubWorldPresenter = new HubWorldPresenter();

        // ── 허브 캠프 ─────────────────────────────────────────

        /// <summary>
        /// 캠프 프롭 생성: 모닥불·포탈·상인·창고·기록실 + 구출로 열리는 시설(대장간·의뢰 게시판).
        /// 탭 상호작용 좌표도 등록한다. 영웅 프롭·선택 모달은 단일 원정자로 가면서 없앴다.
        /// </summary>
        private void CreateHubProps()
        {
            MetaSaveData shelterMeta = MetaStore.LoadOrNew();
            var facilities = new HubFacilitySnapshot(
                shelterMeta.IsFacilityOpen(ShelterFacility.Forge),
                shelterMeta.IsFacilityOpen(ShelterFacility.BountyBoard));
            var context = new HubWorldPresentationContext(
                _grid.Map,
                _grid.iso,
                _visualRoot,
                VisualPosition,
                (owner, renderer, animation) =>
                    AttachEnvironmentAnimator(owner, renderer, animation));
            var visuals = new HubWorldVisuals(
                visualCatalog,
                ActorSprites.GetHubPropSprite,
                ActorSprites.GetCharacterSprite,
                GetHubLightTileSprite);

            _hubWorldPresenter.Present(
                facilities,
                context,
                visuals,
                _hubWorld);

            ApplySurvivorStats();
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
