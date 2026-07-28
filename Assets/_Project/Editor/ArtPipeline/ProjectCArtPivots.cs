using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// 파일명(확장자 제외) → 정규화 피벗의 단일 SSOT.
    /// Aseprite 파이프라인(ProjectCAsepritePipeline)과 PNG 폴백 임포터(ProjectCArtImporter)가
    /// 같은 값을 각자 하드코딩하다 어긋나던 이중 유지를 없앤다 — 피벗을 고칠 일이면 여기만 고친다.
    ///
    /// 값은 정규화 비율이라 해상도 레짐과 무관하다. 표기는 128-레짐(128×64 타일 / PPU 128)
    /// 캔버스 기준 픽셀로 적는다 — 예: 문 캔버스 128×160의 발판 32px = 32f/160f.
    /// </summary>
    public static class ProjectCArtPivots
    {
        private static readonly Vector2 ActorGrounded = new Vector2(0.5f, 0.04f);
        private static readonly Vector2 Centered = new Vector2(0.5f, 0.5f);

        private static readonly Dictionary<string, Vector2> Pivots =
            new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase)
            {
                { "env-floor", Centered },
                { "env-floor-raised", Centered },
                { "env-floor-lower", Centered },
                { "env-floor-mid", Centered },
                { "env-floor-mid-raised", Centered },
                { "env-floor-deep", Centered },
                { "env-floor-deep-raised", Centered },
                { "env-floor-boss", Centered },
                { "env-floor-boss-raised", Centered },
                // hole/weak-floor는 바닥 다이아 경로(GetMappedTileSprite)가 피벗을 재지정하지만,
                // 임포트 규격과 스펙 시트 표기가 어긋나지 않게 중앙으로 명시 등록한다.
                { "env-hole", Centered },
                { "env-weak-floor", Centered },
                // 세워진 사다리 랜드마크 — 절차 아트(GetLadderLandmarkSprite)와 같은 발 기준.
                { "env-ladder", new Vector2(0.5f, 0.08f) },
                { "env-stairs-rising-right", new Vector2(0.5f, 32f / 112f) },
                { "env-stairs-rising-left", new Vector2(0.5f, 32f / 112f) },
                { "env-stairs-up-rising-right", new Vector2(0.5f, 32f / 112f) },
                { "env-stairs-up-rising-left", new Vector2(0.5f, 32f / 112f) },
                { "env-stairs-down-rising-right", new Vector2(0.5f, 32f / 80f) },
                { "env-stairs-down-rising-left", new Vector2(0.5f, 32f / 80f) },
                { "env-door-closed-rising-right", new Vector2(0.5f, 32f / 160f) },
                { "env-door-closed-rising-left", new Vector2(0.5f, 32f / 160f) },
                { "env-door-open-rising-right", new Vector2(0.5f, 32f / 160f) },
                { "env-door-open-rising-left", new Vector2(0.5f, 32f / 160f) },
                { "env-wall-rising-right", new Vector2(0.5f, 16f / 112f) },
                { "env-wall-rising-left", new Vector2(0.5f, 16f / 112f) },
                { "env-wall-torch-rising-right", new Vector2(0.5f, 16f / 112f) },
                { "env-wall-torch-rising-left", new Vector2(0.5f, 16f / 112f) },
                { "actor-player", ActorGrounded },
                { "actor-knight", ActorGrounded },
                { "actor-ranger", ActorGrounded },
                { "actor-alchemist", ActorGrounded },
                { "actor-goblin", ActorGrounded },
                { "actor-skeleton", ActorGrounded },
                { "actor-slime", ActorGrounded },
                { "actor-slinger", ActorGrounded },
                { "actor-arc-drone", ActorGrounded },
                { "actor-grave-warden", ActorGrounded },
                { "actor-merchant", ActorGrounded },
                { "prop-campfire", new Vector2(0.5f, 12f / 128f) },
                { "prop-explosive-barrel", new Vector2(0.5f, 10f / 128f) },
                { "prop-portal", new Vector2(0.5f, 12f / 160f) },
                { "prop-stash", new Vector2(0.5f, 22f / 128f) },
                { "marker-player", Centered },
                { "marker-target", Centered },
                { "item-blast-powder", new Vector2(0.5f, 10f / 64f) },
                { "item-bomb", new Vector2(0.5f, 8f / 64f) },
                { "item-coin-pouch", new Vector2(0.5f, 12f / 64f) },
                { "item-frost-bomb", new Vector2(0.5f, 8f / 64f) },
                { "item-frost-shard", new Vector2(0.5f, 8f / 64f) },
                { "item-gemstone", new Vector2(0.5f, 4f / 64f) },
                { "item-herb", new Vector2(0.5f, 10f / 64f) },
                { "item-oil-flask", new Vector2(0.5f, 8f / 64f) },
                { "item-potion", new Vector2(0.5f, 8f / 64f) },
                { "item-recall-scroll", new Vector2(0.5f, 6f / 64f) },
                { "item-relic", new Vector2(0.5f, 6f / 64f) },
                { "item-throwing-knife", new Vector2(0.5f, 4f / 64f) },
                { "fx-impact-physical", Centered },
                { "fx-impact-fire", Centered },
                { "fx-impact-frost", Centered },
                { "fx-impact-heavy", Centered },
                { "fx-status-burn", Centered },
                { "fx-status-freeze", Centered }
            };

        public static bool TryResolve(string baseName, out Vector2 pivot) =>
            Pivots.TryGetValue(baseName, out pivot);

        public static Vector2 ResolveOrDefault(string baseName, Vector2 fallback) =>
            Pivots.TryGetValue(baseName, out Vector2 pivot) ? pivot : fallback;
    }
}
