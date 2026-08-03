using System.Collections;
using ProjectC.Core;
using UnityEngine;
using static ProjectC.Gameplay.PrototypeSpriteCanvas;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 전투 판정과 분리된 순간 타격·지속 상태이상 표현.
    /// 외부 VFX 에셋이 없어도 읽히도록 픽셀 스프라이트를 런타임 생성한다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private const int CombatFxSortingOrder = OverlaySorting.CombatFx;
        private const string BurnFxName = "Status Burn FX";
        private const string FreezeFxName = "Status Freeze FX";
        private const int MinimumCombatVisualFrames = 4;

        internal static Vector2 ImpactRecoilDirection(Vector2 target, Vector2 origin)
        {
            Vector2 delta = target - origin;
            return delta.sqrMagnitude > 0.000001f ? delta.normalized : Vector2.zero;
        }

        private IEnumerator AnimateMeleeLunge(
            Transform attacker,
            SpriteRenderer attackerRenderer,
            Vector3 targetWorld)
        {
            if (attacker == null) yield break;

            Vector3 start = attacker.position;
            Vector3 direction = targetWorld - start;
            direction.z = 0f;
            direction = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector3.right;
            Vector3 contact = start + Vector3.ClampMagnitude(direction, 0.22f);
            Vector3 originalScale = attacker.localScale;
            Transform art = attackerRenderer != null ? attackerRenderer.transform : null;
            Vector3 originalArtPosition = art != null ? art.localPosition : Vector3.zero;
            Quaternion originalArtRotation = art != null ? art.localRotation : Quaternion.identity;
            Vector3 originalArtScale = art != null ? art.localScale : Vector3.one;

            StartCoroutine(AnimateDirectionalAttackCue(
                start + Vector3.up * 0.46f + direction * 0.18f,
                direction,
                melee: true,
                color: new Color32(255, 194, 82, 255)));

            const float forwardDuration = 0.09f;
            float elapsed = 0f;
            int renderedFrames = 0;
            float t = 0f;
            while (t < 1f)
            {
                if (attacker == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                renderedFrames++;
                t = VisualAnimationProgress(
                    elapsed,
                    forwardDuration,
                    renderedFrames,
                    MinimumCombatVisualFrames);
                float eased = SmoothStep(t);
                attacker.position = Vector3.Lerp(start, contact, eased) +
                                    Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.055f;
                attacker.localScale = new Vector3(
                    originalScale.x * (1f + t * 0.08f),
                    originalScale.y * (1f - t * 0.06f),
                    originalScale.z);
                if (art != null)
                {
                    float punch = Mathf.Sin(t * Mathf.PI * 0.5f);
                    art.localRotation = originalArtRotation * Quaternion.Euler(
                        0f,
                        0f,
                        -direction.x * punch * 11f);
                    art.localPosition = originalArtPosition +
                                        new Vector3(direction.x * punch * 0.025f, 0f, 0f);
                }
                yield return null;
            }

            const float returnDuration = 0.10f;
            elapsed = 0f;
            renderedFrames = 0;
            t = 0f;
            while (t < 1f)
            {
                if (attacker == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                renderedFrames++;
                t = VisualAnimationProgress(
                    elapsed,
                    returnDuration,
                    renderedFrames,
                    MinimumCombatVisualFrames);
                attacker.position = Vector3.Lerp(contact, start, SmoothStep(t));
                attacker.localScale = Vector3.Lerp(
                    new Vector3(
                        originalScale.x * 1.08f,
                        originalScale.y * 0.94f,
                        originalScale.z),
                    originalScale,
                    t);
                if (art != null)
                {
                    art.localPosition = Vector3.Lerp(
                        originalArtPosition + new Vector3(direction.x * 0.025f, 0f, 0f),
                        originalArtPosition,
                        t);
                    art.localRotation = Quaternion.Slerp(
                        originalArtRotation * Quaternion.Euler(0f, 0f, -direction.x * 11f),
                        originalArtRotation,
                        t);
                }
                yield return null;
            }

            if (attacker != null)
            {
                attacker.position = start;
                attacker.localScale = originalScale;
            }
            if (art != null)
            {
                art.localPosition = originalArtPosition;
                art.localRotation = originalArtRotation;
                art.localScale = originalArtScale;
            }
        }

        private IEnumerator AnimateRangedRelease(
            Transform attacker,
            SpriteRenderer attackerRenderer,
            Vector3 targetWorld,
            Color32 cueColor)
        {
            if (attacker == null) yield break;

            Vector3 start = attacker.position;
            Vector3 projectileStart = start + Vector3.up * 0.42f;
            Vector3 direction = targetWorld - projectileStart;
            direction.z = 0f;
            direction = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector3.right;
            Transform art = attackerRenderer != null ? attackerRenderer.transform : null;
            Vector3 originalArtPosition = art != null ? art.localPosition : Vector3.zero;
            Quaternion originalArtRotation = art != null ? art.localRotation : Quaternion.identity;
            Color originalColor = attackerRenderer != null ? attackerRenderer.color : Color.white;

            StartCoroutine(AnimateDirectionalAttackCue(
                start + Vector3.up * 0.48f + direction * 0.28f,
                direction,
                melee: false,
                color: cueColor));

            const float duration = 0.11f;
            float elapsed = 0f;
            int renderedFrames = 0;
            float t = 0f;
            while (t < 1f)
            {
                if (attacker == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                renderedFrames++;
                t = VisualAnimationProgress(
                    elapsed,
                    duration,
                    renderedFrames,
                    MinimumCombatVisualFrames);
                float recoil = Mathf.Sin(t * Mathf.PI);
                attacker.position = start - direction * (recoil * 0.055f);
                if (art != null)
                {
                    art.localPosition = originalArtPosition +
                                        new Vector3(-direction.x * recoil * 0.018f, 0f, 0f);
                    art.localRotation = originalArtRotation * Quaternion.Euler(
                        0f,
                        0f,
                        direction.x * recoil * 6f);
                }
                if (attackerRenderer != null)
                    attackerRenderer.color = Color.Lerp(originalColor, cueColor, recoil * 0.34f);
                yield return null;
            }

            if (attacker != null) attacker.position = start;
            if (art != null)
            {
                art.localPosition = originalArtPosition;
                art.localRotation = originalArtRotation;
            }
            if (attackerRenderer != null) attackerRenderer.color = originalColor;
        }

        private IEnumerator PlayCombatImpact(
            Transform target,
            SpriteRenderer renderer,
            CombatImpactKind kind,
            Vector3? impactOrigin = null)
        {
            if (target == null || renderer == null) yield break;

            Vector2 recoil2 = impactOrigin.HasValue
                ? ImpactRecoilDirection(target.position, impactOrigin.Value)
                : Vector2.zero;
            Vector3 recoilDirection = new Vector3(recoil2.x, recoil2.y, 0f);
            Vector3 burstDirection = recoilDirection.sqrMagnitude > 0.000001f
                ? recoilDirection
                : Vector3.up;
            Vector3 impactPosition = target.position + Vector3.up * 0.48f -
                                     recoilDirection * 0.12f;
            StartCoroutine(AnimateImpactBurst(impactPosition, kind, 1f, burstDirection));
            if (kind == CombatImpactKind.Physical || kind == CombatImpactKind.Heavy)
            {
                StartCoroutine(ShakeCamera(
                    CombatPresentationRules.ShakeStrength(kind),
                    kind == CombatImpactKind.Heavy ? 0.17f : 0.11f));
            }

            Color originalColor = renderer.color;
            Vector3 originalPosition = target.position;
            Vector3 originalScale = target.localScale;
            Color highlight = ImpactHighlight(kind);
            float duration = kind == CombatImpactKind.Heavy ? 0.18f : 0.135f;
            int pulses = CombatPresentationRules.FlashPulses(kind);
            float elapsed = 0f;
            int renderedFrames = 0;
            float t = 0f;

            // 첫 프레임의 정지를 의도적으로 남겨 투사체/공격 모션과 적중 순간을 분리한다.
            yield return new WaitForSeconds(0.022f);

            while (t < 1f)
            {
                if (target == null || renderer == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                renderedFrames++;
                t = VisualAnimationProgress(
                    elapsed,
                    duration,
                    renderedFrames,
                    MinimumCombatVisualFrames);
                float punch = Mathf.Sin(t * Mathf.PI);
                float flicker = Mathf.PingPong(t * pulses * 2f, 1f);
                float jitter = Mathf.Sin(t * Mathf.PI * 6f) *
                               (kind == CombatImpactKind.Heavy ? 0.045f : 0.025f) *
                               (1f - t);
                Vector3 perpendicular = new Vector3(
                    -burstDirection.y,
                    burstDirection.x,
                    0f);
                float recoil = kind == CombatImpactKind.Heavy ? 0.115f : 0.075f;

                target.position = originalPosition +
                                  recoilDirection * (punch * recoil) +
                                  perpendicular * jitter;
                target.localScale = new Vector3(
                    originalScale.x * (1f + punch * 0.11f),
                    originalScale.y * (1f - punch * 0.09f),
                    originalScale.z);
                renderer.color = Color.Lerp(originalColor, highlight, 0.42f + flicker * 0.5f);
                yield return null;
            }

            if (target != null)
            {
                target.position = originalPosition;
                target.localScale = originalScale;
            }
            if (renderer != null) renderer.color = originalColor;
        }

        private IEnumerator AnimateImpactBurst(
            Vector3 worldPosition,
            CombatImpactKind kind,
            float size,
            Vector3 direction = default)
        {
            var burst = new GameObject($"Combat Impact {kind}");
            burst.transform.SetParent(_visualRoot, false);
            burst.transform.position = worldPosition;
            burst.transform.localScale = Vector3.one * (0.36f * size);

            var burstRenderer = burst.AddComponent<SpriteRenderer>();
            burstRenderer.sprite = GetCombatImpactSprite(kind);
            burstRenderer.sortingOrder = CombatFxSortingOrder;

            float baseAngle = direction.sqrMagnitude > 0.000001f
                ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                : 0f;

            float duration = kind == CombatImpactKind.Heavy ? 0.24f : 0.18f;
            float elapsed = 0f;
            int renderedFrames = 0;
            float t = 0f;
            Color color = Color.white;
            while (t < 1f)
            {
                if (burst == null || burstRenderer == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                renderedFrames++;
                t = VisualAnimationProgress(
                    elapsed,
                    duration,
                    renderedFrames,
                    MinimumCombatVisualFrames);
                float scale = Mathf.Lerp(0.36f, 1.28f, SmoothStep(t)) * size;
                float directionalStretch = direction.sqrMagnitude > 0.000001f ? 1.22f : 1f;
                burst.transform.localScale = new Vector3(
                    scale * directionalStretch,
                    scale / directionalStretch,
                    1f);
                burst.transform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    baseAngle + t * 18f);
                color.a = 1f - t * t;
                burstRenderer.color = color;
                yield return null;
            }
            if (burst != null) Destroy(burst);
        }

        private IEnumerator AnimateDirectionalAttackCue(
            Vector3 worldPosition,
            Vector3 direction,
            bool melee,
            Color color)
        {
            var cue = new GameObject(melee ? "Melee Direction Arc" : "Ranged Muzzle Flash");
            cue.transform.SetParent(_visualRoot, false);
            cue.transform.position = worldPosition;
            cue.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            var cueRenderer = cue.AddComponent<SpriteRenderer>();
            cueRenderer.sprite = GetDirectionalAttackCueSprite(melee);
            cueRenderer.sortingOrder = CombatFxSortingOrder;

            const float duration = 0.16f;
            float elapsed = 0f;
            int renderedFrames = 0;
            float t = 0f;
            while (t < 1f)
            {
                if (cue == null || cueRenderer == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                renderedFrames++;
                t = VisualAnimationProgress(
                    elapsed,
                    duration,
                    renderedFrames,
                    MinimumCombatVisualFrames);
                float scale = Mathf.Lerp(melee ? 0.48f : 0.30f, melee ? 1.15f : 0.82f, SmoothStep(t));
                cue.transform.localScale = new Vector3(scale, scale, 1f);
                Color faded = color;
                faded.a = 1f - t * t;
                cueRenderer.color = faded;
                yield return null;
            }

            if (cue != null) Destroy(cue);
        }

        private Sprite GetDirectionalAttackCueSprite(bool melee)
        {
            string key = melee ? "combat-direction-melee" : "combat-direction-ranged";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(32, 24);
            Color32 edge = melee
                ? new Color32(255, 132, 44, 230)
                : new Color32(38, 199, 212, 235);
            Color32 core = melee
                ? new Color32(255, 232, 148, 255)
                : new Color32(219, 255, 255, 255);
            if (melee)
            {
                for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    float dx = x - 5f;
                    float dy = y - 11.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Abs(Mathf.Atan2(dy, dx));
                    if (radius >= 8f && radius <= 15f && angle <= 0.64f)
                        texture.SetPixel(x, y, radius >= 13.5f || angle >= 0.52f ? edge : core);
                }
            }
            else
            {
                for (int x = 3; x < 30; x++)
                {
                    int halfWidth = Mathf.Max(0, 4 - x / 7);
                    for (int y = 12 - halfWidth; y <= 12 + halfWidth; y++)
                        texture.SetPixel(x, y, x > 24 || y == 12 ? core : edge);
                }
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.12f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private IEnumerator ShakeCamera(float strength, float duration)
        {
            Camera camera = Camera.main;
            if (camera == null || strength <= 0f || duration <= 0f) yield break;

            Transform cameraTransform = camera.transform;
            Vector3 origin = cameraTransform.localPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (cameraTransform == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float damping = 1f - t;
                float x = Mathf.Sin(t * 41f) * strength * damping;
                float y = Mathf.Cos(t * 53f) * strength * 0.55f * damping;
                cameraTransform.localPosition = origin + new Vector3(x, y, 0f);
                yield return null;
            }
            if (cameraTransform != null) cameraTransform.localPosition = origin;
        }

        private StatusApplyResult ApplyStatusWithPresentation(
            CombatantState target,
            StatusKind kind,
            int turns)
        {
            StatusApplyResult result = target.Statuses.Apply(kind, turns);
            PresentStatusApplied(target, kind, result);
            return result;
        }

        /// <summary>
        /// 이미 부여된 상태이상의 연출만 낸다.
        /// <para>
        /// 부여와 연출이 한 함수에 붙어 있으면 <see cref="HazardSequence"/>처럼 Core가 먼저
        /// 부여한 결과를 화면에만 반영할 수 없다 — 두 번 부여하게 된다.
        /// </para>
        /// </summary>
        private StatusApplyResult PresentStatusApplied(
            CombatantState target,
            StatusKind kind,
            StatusApplyResult result)
        {
            if (result == StatusApplyResult.Applied || result == StatusApplyResult.Refreshed)
                _runTelemetry?.RecordStatus(kind);
            EnemyAgent enemy = target == _playerState ? null : FindAgentByState(target);
            bool visible = target == _playerState || enemy != null && IsEnemyVisibleToPlayer(enemy);

            if (target == _playerState)
                SyncPlayerStatusVisuals();
            else if (enemy != null)
                ApplyEnemyVisuals(enemy);

            if (!visible) return result;

            Vector3 worldPosition = target == _playerState
                ? _player.transform.position
                : enemy.Root != null
                    ? enemy.Root.transform.position
                    : _grid.GridToWorld(target.Position);
            CombatImpactKind impact = kind == StatusKind.Burn
                ? CombatImpactKind.Fire
                : CombatImpactKind.Frost;
            FloatingTextKind textKind = kind == StatusKind.Burn
                ? FloatingTextKind.Burn
                : FloatingTextKind.Freeze;
            FloatingText?.Show(
                worldPosition,
                CombatPresentationRules.StatusCue(kind, result),
                result == StatusApplyResult.CancelledOpposite
                    ? FloatingTextKind.Alert
                    : textKind);
            StartCoroutine(AnimateImpactBurst(
                worldPosition + Vector3.up * 0.42f,
                impact,
                result == StatusApplyResult.CancelledOpposite ? 0.72f : 0.9f));
            return result;
        }

        private void SyncPlayerStatusVisuals()
        {
            if (_player == null || _playerRenderer == null || _playerState == null) return;

            bool active = _playerState.IsAlive;
            SyncStatusIcon(
                _player,
                _playerRenderer,
                StatusKind.Burn,
                active && _playerState.Statuses.Has(StatusKind.Burn));
            SyncStatusIcon(
                _player,
                _playerRenderer,
                StatusKind.Freeze,
                active && _playerState.Statuses.Has(StatusKind.Freeze));
            if (active) ApplyPlayerVisuals();
        }

        private void SyncEnemyStatusVisuals(EnemyAgent enemy, bool visible)
        {
            if (enemy?.Root == null || enemy.Renderer == null) return;
            bool active = visible && enemy.State.IsAlive;
            SyncStatusIcon(
                enemy.Root,
                enemy.Renderer,
                StatusKind.Burn,
                active && enemy.State.Statuses.Has(StatusKind.Burn));
            SyncStatusIcon(
                enemy.Root,
                enemy.Renderer,
                StatusKind.Freeze,
                active && enemy.State.Statuses.Has(StatusKind.Freeze));
        }

        private void SyncStatusIcon(
            GameObject owner,
            SpriteRenderer actorRenderer,
            StatusKind kind,
            bool active)
        {
            string objectName = kind == StatusKind.Burn ? BurnFxName : FreezeFxName;
            Transform child = owner.transform.Find(objectName);
            if (child == null && !active) return;

            if (child == null)
            {
                var effect = new GameObject(objectName);
                effect.transform.SetParent(owner.transform, false);
                child = effect.transform;

                var renderer = effect.AddComponent<SpriteRenderer>();
                renderer.sprite = GetCombatStatusSprite(kind);
                renderer.sortingOrder = Mathf.Max(
                    CombatFxSortingOrder,
                    actorRenderer.sortingOrder + 3);
                Vector3 basePosition = new Vector3(0f, 0.12f, 0f);
                effect.AddComponent<CombatStatusFxAnimator>()
                    .Configure(renderer, kind, basePosition);
                child.localPosition = basePosition;
            }

            child.gameObject.SetActive(active);
        }

        private Sprite GetCombatStatusSprite(StatusKind kind)
        {
            Sprite art = visualCatalog != null ? visualCatalog.StatusFx(kind) : null;
            if (art != null) return art;

            string key = kind == StatusKind.Burn ? "combat-status-burn" : "combat-status-freeze";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(32, 24);
            if (kind == StatusKind.Burn)
            {
                Color32 outline = new Color32(122, 38, 18, 225);
                Color32 flame = new Color32(244, 88, 30, 245);
                Color32 core = new Color32(255, 213, 70, 255);
                for (int y = 1; y < 13; y++)
                for (int x = 1; x < 31; x++)
                {
                    float ellipse = Mathf.Sqrt(
                        Mathf.Pow((x - 15.5f) / 15f, 2f) +
                        Mathf.Pow((y - 6f) / 5.5f, 2f));
                    if (ellipse >= 0.72f && ellipse <= 1f)
                        texture.SetPixel(x, y, (x + y) % 3 == 0 ? core : outline);
                }

                foreach (int center in new[] { 7, 16, 25 })
                {
                    for (int y = 5; y < 22; y++)
                    {
                        int height = y - 5;
                        int halfWidth = Mathf.Max(1, 4 - height / 5);
                        int sway = ((height + center) / 3) % 2;
                        for (int x = center - halfWidth + sway; x <= center + halfWidth; x++)
                        {
                            bool edge = x == center - halfWidth + sway ||
                                        x == center + halfWidth ||
                                        y >= 20;
                            texture.SetPixel(x, y, edge ? outline : y < 11 ? core : flame);
                        }
                    }
                }
            }
            else
            {
                Color32 edge = new Color32(31, 117, 166, 235);
                Color32 ice = new Color32(98, 216, 245, 245);
                Color32 core = new Color32(224, 252, 255, 255);
                for (int y = 1; y < 14; y++)
                for (int x = 1; x < 31; x++)
                {
                    float diamond = Mathf.Abs((x - 15.5f) / 15f) +
                                    Mathf.Abs((y - 7f) / 6f);
                    if (diamond >= 0.72f && diamond <= 1.03f)
                        texture.SetPixel(x, y, (x + y) % 4 == 0 ? core : edge);
                }

                int[] spikes = { 4, 10, 16, 22, 28 };
                for (int index = 0; index < spikes.Length; index++)
                {
                    int center = spikes[index];
                    int top = 15 + (index % 3) * 3;
                    for (int y = 7; y <= top; y++)
                    {
                        int halfWidth = Mathf.Max(0, 2 - (y - 7) / 4);
                        for (int x = center - halfWidth; x <= center + halfWidth; x++)
                            texture.SetPixel(x, y, y >= top - 1 ? core : ice);
                    }
                }
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private Sprite GetCombatImpactSprite(CombatImpactKind kind)
        {
            Sprite art = visualCatalog != null ? visualCatalog.ImpactFx(kind) : null;
            if (art != null) return art;

            string key = $"combat-impact-{kind}";
            if (_spriteCache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(24, 24);
            ImpactPalette(kind, out Color32 outer, out Color32 middle, out Color32 core);
            int rays = CombatPresentationRules.BurstRayCount(kind);
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
            {
                float dx = x - 11.5f;
                float dy = y - 11.5f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                bool ray = Mathf.Abs(Mathf.Sin(angle * rays * 0.5f)) > 0.88f;
                if (radius <= 2.4f)
                    texture.SetPixel(x, y, core);
                else if (radius <= 5.2f)
                    texture.SetPixel(x, y, middle);
                else if (ray && radius <= 11f)
                    texture.SetPixel(x, y, outer);
            }

            texture.Apply(false, true);
            cached = CreateSprite(texture, new Vector2(0.5f, 0.5f));
            _spriteCache[key] = cached;
            return cached;
        }

        private static Color ImpactHighlight(CombatImpactKind kind)
        {
            switch (kind)
            {
                case CombatImpactKind.Fire: return new Color32(255, 181, 62, 255);
                case CombatImpactKind.Frost: return new Color32(189, 244, 255, 255);
                case CombatImpactKind.Heavy: return new Color32(255, 83, 70, 255);
                default: return Color.white;
            }
        }

        private static FloatingTextKind FloatingKindForImpact(CombatImpactKind kind)
        {
            switch (kind)
            {
                case CombatImpactKind.Fire: return FloatingTextKind.Burn;
                case CombatImpactKind.Frost: return FloatingTextKind.Freeze;
                case CombatImpactKind.Heavy: return FloatingTextKind.HeavyDamage;
                default: return FloatingTextKind.EnemyDamage;
            }
        }

        private static Color CombatantTint(CombatantState state)
        {
            if (!state.IsAlive) return new Color32(60, 64, 66, 180);
            if (state.Statuses.Has(StatusKind.Freeze)) return new Color32(140, 210, 235, 255);
            if (state.Statuses.Has(StatusKind.Burn)) return new Color32(255, 168, 112, 255);
            return Color.white;
        }

        private static void ImpactPalette(
            CombatImpactKind kind,
            out Color32 outer,
            out Color32 middle,
            out Color32 core)
        {
            switch (kind)
            {
                case CombatImpactKind.Fire:
                    outer = new Color32(215, 55, 24, 240);
                    middle = new Color32(255, 128, 35, 255);
                    core = new Color32(255, 239, 145, 255);
                    return;
                case CombatImpactKind.Frost:
                    outer = new Color32(45, 139, 192, 240);
                    middle = new Color32(111, 222, 247, 255);
                    core = new Color32(230, 253, 255, 255);
                    return;
                case CombatImpactKind.Heavy:
                    outer = new Color32(122, 31, 30, 240);
                    middle = new Color32(244, 68, 55, 255);
                    core = new Color32(255, 224, 180, 255);
                    return;
                default:
                    outer = new Color32(200, 115, 62, 235);
                    middle = new Color32(255, 212, 126, 255);
                    core = Color.white;
                    return;
            }
        }
    }
}
