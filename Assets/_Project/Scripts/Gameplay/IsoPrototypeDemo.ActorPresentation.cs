using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 액터의 월드 방향을 보존하고 현재 아이소 시점의 4방향 표현으로 바꾸는 프레젠테이션 계층.
    /// 정식 방향별 클립이 없으면 기존 정적 컷을 좌우 반전 + 전후 원근으로 구분한다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private ActorFacing4 _playerWorldFacing = ActorFacing4.South;

        internal readonly struct StaticFacingPose
        {
            public bool FlipX { get; }
            public Vector2 Scale { get; }
            public Vector2 Offset { get; }

            public StaticFacingPose(bool flipX, Vector2 scale, Vector2 offset)
            {
                FlipX = flipX;
                Scale = scale;
                Offset = offset;
            }
        }

        /// <summary>
        /// 현재 단일 우측향 컷을 위한 4방향 폴백. East/North는 카메라 쪽(화면 아래),
        /// South/West는 반대쪽(화면 위)이라 후자를 살짝 줄여 같은 좌우 실루엣도 구분한다.
        /// </summary>
        internal static StaticFacingPose StaticFacingPoseFor(ActorFacing4 viewFacing)
        {
            bool projectsRight =
                viewFacing == ActorFacing4.East || viewFacing == ActorFacing4.South;
            bool towardCamera =
                viewFacing == ActorFacing4.East || viewFacing == ActorFacing4.North;
            return new StaticFacingPose(
                flipX: !projectsRight,
                scale: towardCamera ? Vector2.one : new Vector2(0.96f, 0.94f),
                offset: towardCamera ? Vector2.zero : new Vector2(0f, 0.025f));
        }

        private ActorFacing4 ViewFacing(ActorFacing4 worldFacing)
        {
            int viewQuarterTurns = _grid != null ? _grid.iso.viewQuarterTurns : 0;
            return ActorFacingRules.RotateToView(worldFacing, viewQuarterTurns);
        }

        private void FacePlayerTowards(GridPos target)
        {
            if (_playerState != null &&
                ActorFacingRules.TryResolveWorld(
                    _playerState.Position,
                    target,
                    out ActorFacing4 facing))
                _playerWorldFacing = facing;
            ApplyPlayerFacing();
        }

        private void FaceEnemyTowards(EnemyAgent enemy, GridPos target)
        {
            if (enemy == null) return;
            if (ActorFacingRules.TryResolveWorld(
                    enemy.State.Position,
                    target,
                    out ActorFacing4 facing))
                enemy.WorldFacing = facing;
            ApplyEnemyFacing(enemy);
        }

        private void ApplyPlayerFacing()
        {
            if (_playerRenderer == null) return;
            ActorFacing4 facing = ViewFacing(_playerWorldFacing);
            _playerAnimator?.SetFacing(facing);
            ApplyFacingPose(
                _playerRenderer,
                facing,
                playerVisualScale,
                _playerAnimator != null && _playerAnimator.HasDirectionalClips);
        }

        private void ApplyEnemyFacing(EnemyAgent enemy)
        {
            if (enemy?.Renderer == null) return;
            ActorFacing4 facing = ViewFacing(enemy.WorldFacing);
            enemy.Animator?.SetFacing(facing);
            ApplyFacingPose(
                enemy.Renderer,
                facing,
                actorVisualScale,
                enemy.Animator != null && enemy.Animator.HasDirectionalClips);
        }

        private static void ApplyFacingPose(
            SpriteRenderer renderer,
            ActorFacing4 facing,
            float visualScale,
            bool hasDirectionalClips)
        {
            Transform art = renderer.transform;
            if (hasDirectionalClips)
            {
                renderer.flipX = false;
                art.localPosition = Vector3.zero;
                art.localRotation = Quaternion.identity;
                art.localScale = Vector3.one * visualScale;
                return;
            }

            StaticFacingPose pose = StaticFacingPoseFor(facing);
            renderer.flipX = pose.FlipX;
            art.localPosition = new Vector3(pose.Offset.x, pose.Offset.y, 0f);
            art.localRotation = Quaternion.identity;
            art.localScale = new Vector3(
                visualScale * pose.Scale.x,
                visualScale * pose.Scale.y,
                visualScale);
        }

        private void RefreshActorFacings()
        {
            ApplyPlayerFacing();
            foreach (EnemyAgent enemy in _enemies)
                ApplyEnemyFacing(enemy);
        }
    }
}
