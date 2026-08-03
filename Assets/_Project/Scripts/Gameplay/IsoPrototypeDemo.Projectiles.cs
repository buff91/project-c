using System.Collections;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>사격·투척·폭발의 순간 월드 연출. 판정과 아이템 소비는 Actions/Falls가 소유한다.</summary>
    public partial class IsoPrototypeDemo
    {
        private enum ProjectileVisualKind
        {
            ArcBolt,
            HostileBolt,
            Knife,
            Bomb,
            FrostBomb,
            OilFlask
        }

        private IEnumerator AnimateBlast(GridPos center, bool fiery = true)
        {
            CombatImpactKind impact = fiery
                ? CombatImpactKind.Fire
                : CombatImpactKind.Frost;
            StartCoroutine(ShakeCamera(
                CombatPresentationRules.ShakeStrength(impact) * 1.55f,
                fiery ? 0.18f : 0.14f));

            var blast = new GameObject("Bomb Blast");
            blast.transform.SetParent(_visualRoot, false);
            blast.transform.position = VisualPosition(center) + Vector3.up * 0.18f;
            var renderer = blast.AddComponent<SpriteRenderer>();
            renderer.sprite = ActorSprites.GetBlastSprite(fiery);
            renderer.sortingOrder = OverlaySorting.Blast;

            float elapsed = 0f;
            const float duration = 0.24f;
            int renderedFrames = 0;
            float t = 0f;
            while (t < 1f)
            {
                elapsed += Time.unscaledDeltaTime;
                renderedFrames++;
                t = VisualAnimationProgress(
                    elapsed,
                    duration,
                    renderedFrames,
                    MinimumCombatVisualFrames);
                float scale = Mathf.Lerp(0.5f, 2.6f, SmoothStep(t));
                blast.transform.localScale = new Vector3(scale, scale, 1f);
                Color color = renderer.color;
                color.a = 1f - t * t;
                renderer.color = color;
                yield return null;
            }

            Destroy(blast);
        }

        /// <summary>
        /// 같은 층은 한 번의 포물선, 층간 투척은 near endpoint → 수직 통과 → far endpoint →
        /// 목표의 세 구간으로 재생한다. 판정이 Hole을 경유하는데 그림만 대각선 직사로 보이지 않게 한다.
        /// </summary>
        private IEnumerator AnimateProjectile(
            GridPos from,
            GridPos to,
            VerticalThrowPath? verticalPath = null,
            ProjectileVisualKind visualKind = ProjectileVisualKind.ArcBolt)
        {
            var projectile = new GameObject($"Projectile {visualKind}");
            projectile.transform.SetParent(_visualRoot, false);
            var renderer = projectile.AddComponent<SpriteRenderer>();
            renderer.sprite = ProjectileSprite(visualKind);
            renderer.color = ProjectileColor(visualKind);
            renderer.sortingOrder = OverlaySorting.Projectile;
            projectile.transform.localScale = Vector3.one * ProjectileScale(visualKind);

            Vector3 start = ProjectileEndpointWorld(from, isVerticalEndpoint: false);
            if (verticalPath.HasValue && _dungeon != null)
            {
                VerticalThrowPath path = verticalPath.Value;
                bool downward =
                    _dungeon.Height.FloorIndex(from.elevation) ==
                    _dungeon.Height.FloorIndex(path.Opening.elevation);
                GridPos near = downward ? path.Opening : path.Landing;
                GridPos far = downward ? path.Landing : path.Opening;
                Vector3 nearWorld = ProjectileEndpointWorld(near, isVerticalEndpoint: true);
                Vector3 farWorld = ProjectileEndpointWorld(far, isVerticalEndpoint: true);
                Vector3 endWorld = ProjectileEndpointWorld(to, isVerticalEndpoint: false);

                yield return AnimateProjectileSegment(
                    projectile.transform, start, nearWorld, 0.13f,
                    arcHeight: 0.18f, visualKind: visualKind);
                yield return AnimateProjectileSegment(
                    projectile.transform, nearWorld, farWorld, 0.10f,
                    arcHeight: 0f, visualKind: visualKind);
                yield return AnimateProjectileSegment(
                    projectile.transform, farWorld, endWorld, 0.13f,
                    arcHeight: 0.18f, visualKind: visualKind);
            }
            else
            {
                Vector3 end = ProjectileEndpointWorld(to, isVerticalEndpoint: false);
                yield return AnimateProjectileSegment(
                    projectile.transform, start, end, 0.20f,
                    arcHeight: 0.24f, visualKind: visualKind);
            }

            Destroy(projectile);
        }

        /// <summary>
        /// 수직 투척은 최종 목표보다 먼저 현재 층의 개구부로 날아간다. 발사 포즈와 총구 큐도
        /// 실제 첫 구간을 가리키도록 동일한 near endpoint 계약을 공유한다.
        /// </summary>
        private GridPos ProjectileReleaseTarget(
            GridPos from,
            GridPos to,
            VerticalThrowPath? verticalPath)
        {
            if (!verticalPath.HasValue || _dungeon == null) return to;

            VerticalThrowPath path = verticalPath.Value;
            bool downward =
                _dungeon.Height.FloorIndex(from.elevation) ==
                _dungeon.Height.FloorIndex(path.Opening.elevation);
            return downward ? path.Opening : path.Landing;
        }

        /// <summary>
        /// 발사 큐와 투사체가 같은 첫 구간 끝점을 바라보게 한다. 층간 개구부는 통과 중심(0.30),
        /// 일반 목표는 액터/아이템 발사 높이(0.42)를 쓴다.
        /// </summary>
        private Vector3 ProjectileReleaseWorldTarget(
            GridPos from,
            GridPos to,
            VerticalThrowPath? verticalPath)
        {
            GridPos target = ProjectileReleaseTarget(from, to, verticalPath);
            return ProjectileEndpointWorld(target, verticalPath.HasValue && _dungeon != null);
        }

        private Vector3 ProjectileEndpointWorld(GridPos pos, bool isVerticalEndpoint)
        {
            return VisualPosition(pos) + Vector3.up * (isVerticalEndpoint ? 0.30f : 0.42f);
        }

        private Sprite ProjectileSprite(ProjectileVisualKind kind)
        {
            ItemKind? item = kind switch
            {
                ProjectileVisualKind.Knife => ItemKind.ThrowingKnife,
                ProjectileVisualKind.Bomb => ItemKind.Bomb,
                ProjectileVisualKind.FrostBomb => ItemKind.FrostBomb,
                ProjectileVisualKind.OilFlask => ItemKind.OilFlask,
                _ => (ItemKind?)null
            };
            if (item.HasValue)
            {
                Sprite mapped = visualCatalog != null ? visualCatalog.ItemFor(item.Value) : null;
                return mapped != null ? mapped : ActorSprites.GetItemSprite(item.Value);
            }

            return ActorSprites.GetProjectileSprite();
        }

        private static Color ProjectileColor(ProjectileVisualKind kind)
        {
            switch (kind)
            {
                case ProjectileVisualKind.HostileBolt:
                    return new Color32(238, 75, 126, 255);
                case ProjectileVisualKind.ArcBolt:
                    return new Color32(92, 239, 241, 255);
                default:
                    return Color.white;
            }
        }

        private static float ProjectileScale(ProjectileVisualKind kind)
        {
            switch (kind)
            {
                case ProjectileVisualKind.Bomb:
                case ProjectileVisualKind.FrostBomb:
                case ProjectileVisualKind.OilFlask:
                    return 0.42f;
                case ProjectileVisualKind.Knife:
                    return 0.52f;
                default:
                    return 0.78f;
            }
        }

        private static IEnumerator AnimateProjectileSegment(
            Transform projectile,
            Vector3 start,
            Vector3 end,
            float duration,
            float arcHeight,
            ProjectileVisualKind visualKind)
        {
            float elapsed = 0f;
            int renderedFrames = 0;
            float t = 0f;
            Vector3 travel = end - start;
            float angle = Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg;
            while (t < 1f)
            {
                if (projectile == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                renderedFrames++;
                t = VisualAnimationProgress(
                    elapsed,
                    duration,
                    renderedFrames,
                    MinimumCombatVisualFrames);
                projectile.position = Vector3.Lerp(start, end, t) +
                                      Vector3.up * (Mathf.Sin(t * Mathf.PI) * arcHeight);
                float spin = visualKind == ProjectileVisualKind.Bomb ||
                             visualKind == ProjectileVisualKind.FrostBomb ||
                             visualKind == ProjectileVisualKind.OilFlask
                    ? t * 220f
                    : 0f;
                projectile.localRotation = Quaternion.Euler(0f, 0f, angle + spin);
                yield return null;
            }
            if (projectile != null) projectile.position = end;
        }
    }
}
