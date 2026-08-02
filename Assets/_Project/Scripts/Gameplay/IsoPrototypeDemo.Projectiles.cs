using System.Collections;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>사격·투척·폭발의 순간 월드 연출. 판정과 아이템 소비는 Actions/Falls가 소유한다.</summary>
    public partial class IsoPrototypeDemo
    {
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
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
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
            VerticalThrowPath? verticalPath = null)
        {
            var projectile = new GameObject("Ranged Projectile");
            projectile.transform.SetParent(_visualRoot, false);
            var renderer = projectile.AddComponent<SpriteRenderer>();
            renderer.sprite = ActorSprites.GetProjectileSprite();
            renderer.sortingOrder = OverlaySorting.Projectile;

            Vector3 start = VisualPosition(from) + Vector3.up * 0.42f;
            if (verticalPath.HasValue && _dungeon != null)
            {
                VerticalThrowPath path = verticalPath.Value;
                bool downward =
                    _dungeon.Height.FloorIndex(from.elevation) ==
                    _dungeon.Height.FloorIndex(path.Opening.elevation);
                GridPos near = downward ? path.Opening : path.Landing;
                GridPos far = downward ? path.Landing : path.Opening;
                Vector3 nearWorld = VisualPosition(near) + Vector3.up * 0.30f;
                Vector3 farWorld = VisualPosition(far) + Vector3.up * 0.30f;
                Vector3 endWorld = VisualPosition(to) + Vector3.up * 0.42f;

                yield return AnimateProjectileSegment(
                    projectile.transform, start, nearWorld, 0.13f, arcHeight: 0.18f);
                yield return AnimateProjectileSegment(
                    projectile.transform, nearWorld, farWorld, 0.10f, arcHeight: 0f);
                yield return AnimateProjectileSegment(
                    projectile.transform, farWorld, endWorld, 0.13f, arcHeight: 0.18f);
            }
            else
            {
                Vector3 end = VisualPosition(to) + Vector3.up * 0.42f;
                yield return AnimateProjectileSegment(
                    projectile.transform, start, end, 0.20f, arcHeight: 0.24f);
            }

            Destroy(projectile);
        }

        private static IEnumerator AnimateProjectileSegment(
            Transform projectile,
            Vector3 start,
            Vector3 end,
            float duration,
            float arcHeight)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                projectile.position = Vector3.Lerp(start, end, t) +
                                      Vector3.up * (Mathf.Sin(t * Mathf.PI) * arcHeight);
                yield return null;
            }
            projectile.position = end;
        }
    }
}
