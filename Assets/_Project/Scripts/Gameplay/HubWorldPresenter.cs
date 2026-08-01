using System;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 허브를 그리는 데 필요한 메타 진행의 불변 스냅샷. Presenter가 세이브 저장소를
    /// 직접 읽지 않게 해, 같은 씬 인스턴스를 재빌드해도 호출 시점의 값만 반영한다.
    /// </summary>
    internal readonly struct HubFacilitySnapshot
    {
        public bool ForgeOpen { get; }
        public bool BountyBoardOpen { get; }

        public HubFacilitySnapshot(bool forgeOpen, bool bountyBoardOpen)
        {
            ForgeOpen = forgeOpen;
            BountyBoardOpen = bountyBoardOpen;
        }
    }

    internal enum HubPropKind
    {
        Campfire = 0,
        Portal = 1,
        Merchant = 2,
        Stash = 3,
        Smith = 4,
        BountyBoard = 5,
        Codex = 6
    }

    /// <summary>
    /// 허브 논리 프롭을 카탈로그 우선 스프라이트와 절차 폴백으로 해석한다.
    /// Presenter는 카탈로그 필드명이나 폴백 키를 알지 않는다.
    /// </summary>
    internal sealed class HubWorldVisuals
    {
        private readonly IsoVisualCatalog _catalog;
        private readonly Func<string, Sprite> _hubPropFallback;
        private readonly Func<bool, Sprite> _characterFallback;
        private readonly Func<string, int, Sprite> _lightTile;

        public HubWorldVisuals(
            IsoVisualCatalog catalog,
            Func<string, Sprite> hubPropFallback,
            Func<bool, Sprite> characterFallback,
            Func<string, int, Sprite> lightTile)
        {
            _catalog = catalog;
            _hubPropFallback = hubPropFallback ??
                               throw new ArgumentNullException(nameof(hubPropFallback));
            _characterFallback = characterFallback ??
                                 throw new ArgumentNullException(nameof(characterFallback));
            _lightTile = lightTile ??
                         throw new ArgumentNullException(nameof(lightTile));
        }

        public Sprite GetPropSprite(HubPropKind kind)
        {
            switch (kind)
            {
                case HubPropKind.Campfire:
                    return CatalogOrFallback(
                        _catalog != null ? _catalog.hubCampfire : null,
                        "campfire");
                case HubPropKind.Portal:
                    return CatalogOrFallback(
                        _catalog != null ? _catalog.hubPortal : null,
                        "portal");
                case HubPropKind.Merchant:
                {
                    Sprite mapped = _catalog != null ? _catalog.merchant : null;
                    return mapped != null ? mapped : _characterFallback(true);
                }
                case HubPropKind.Stash:
                    return CatalogOrFallback(
                        _catalog != null ? _catalog.hubStash : null,
                        "stash");
                case HubPropKind.Smith:
                    return _hubPropFallback("smith");
                case HubPropKind.BountyBoard:
                    return _hubPropFallback("bounty");
                case HubPropKind.Codex:
                    return _hubPropFallback("codex");
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public Sprite GetLightSprite(string kind, int strength) =>
            _lightTile(kind, strength);

        public EnvironmentAnimationSet GetAnimationSet(string slotKey) =>
            _catalog != null ? _catalog.EnvironmentAnimationsFor(slotKey) : null;

        private Sprite CatalogOrFallback(Sprite mapped, string fallbackKey) =>
            mapped != null ? mapped : _hubPropFallback(fallbackKey);
    }

    /// <summary>
    /// Presenter가 알아야 하는 씬 기반 시설만 묶는다. GameObject 수명은 Root를 만든
    /// <see cref="IsoPrototypeDemo"/>가, 좌표 변환은 주입된 VisualPosition이 계속 소유한다.
    /// </summary>
    internal sealed class HubWorldPresentationContext
    {
        public GridMap Map { get; }
        public IsoGrid Iso { get; }
        public Transform Root { get; }
        public Func<GridPos, Vector3> VisualPosition { get; }
        public Action<GameObject, SpriteRenderer, EnvironmentAnimationSet> AttachAnimation { get; }

        public HubWorldPresentationContext(
            GridMap map,
            IsoGrid iso,
            Transform root,
            Func<GridPos, Vector3> visualPosition,
            Action<GameObject, SpriteRenderer, EnvironmentAnimationSet> attachAnimation)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Iso = iso ?? throw new ArgumentNullException(nameof(iso));
            Root = root != null ? root : throw new ArgumentNullException(nameof(root));
            VisualPosition = visualPosition ??
                             throw new ArgumentNullException(nameof(visualPosition));
            AttachAnimation = attachAnimation ??
                              throw new ArgumentNullException(nameof(attachAnimation));
        }
    }

    /// <summary>
    /// 시설 개방 스냅샷을 허브 프롭·광원 GameObject와 상호작용 등록으로 표현한다.
    /// 메타 저장, 플레이어 상태, Generated Visuals 초기화는 알지 않는다.
    /// </summary>
    internal sealed class HubWorldPresenter
    {
        public void Present(
            HubFacilitySnapshot facilities,
            HubWorldPresentationContext context,
            HubWorldVisuals visuals,
            HubWorldRegistry registry)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (visuals == null) throw new ArgumentNullException(nameof(visuals));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            CreateLightPatch("campfire", HubLayout.Campfire, 2, context, visuals, registry);
            CreateLightPatch("portal", HubLayout.Portal, 1, context, visuals, registry);

            CreateProp(
                "Campfire",
                HubPropKind.Campfire,
                HubLayout.Campfire,
                context,
                visuals,
                registry,
                animationKey: "hubCampfire");
            CreateProp(
                "Portal",
                HubPropKind.Portal,
                HubLayout.Portal,
                context,
                visuals,
                registry,
                animationKey: "hubPortal");
            CreateProp(
                "Merchant",
                HubPropKind.Merchant,
                HubLayout.Merchant,
                context,
                visuals,
                registry,
                interaction: new HubInteractionTarget("merchant", "상인"));
            CreateProp(
                "Stash",
                HubPropKind.Stash,
                HubLayout.Stash,
                context,
                visuals,
                registry,
                interaction: new HubInteractionTarget("stash", "창고"));

            // 구출로 열리는 시설은 프롭과 상호작용을 같은 분기에서 함께 만든다.
            // 둘을 따로 조건부 처리하면 보이지 않는 클릭 칸이나 클릭 불가 프롭이 남는다.
            if (facilities.ForgeOpen)
            {
                CreateProp(
                    "Smith",
                    HubPropKind.Smith,
                    HubLayout.Smith,
                    context,
                    visuals,
                    registry,
                    interaction: new HubInteractionTarget("smith", "대장간"));
            }

            if (facilities.BountyBoardOpen)
            {
                CreateProp(
                    "BountyBoard",
                    HubPropKind.BountyBoard,
                    HubLayout.BountyBoard,
                    context,
                    visuals,
                    registry,
                    interaction: new HubInteractionTarget("bounty", "의뢰 게시판"));
            }

            // 해금 조건을 배우는 기록실은 잠기는 시설에 포함하지 않는다.
            CreateProp(
                "Codex",
                HubPropKind.Codex,
                HubLayout.Codex,
                context,
                visuals,
                registry,
                interaction: new HubInteractionTarget("codex", "기록실"));
        }

        private static void CreateLightPatch(
            string kind,
            GridPos origin,
            int radius,
            HubWorldPresentationContext context,
            HubWorldVisuals visuals,
            HubWorldRegistry registry)
        {
            foreach (KeyValuePair<GridPos, TileData> pair in context.Map.All())
            {
                GridPos position = pair.Key;
                if (!pair.Value.IsWalkable || position.elevation != origin.elevation)
                    continue;

                int distance = position.ManhattanTo(origin);
                if (distance > radius) continue;

                int strength = distance == 0 ? 3 : distance == 1 ? 2 : 1;
                var lightTile = new GameObject($"{kind} Light {position.x},{position.y}");
                lightTile.transform.SetParent(context.Root, false);
                lightTile.transform.position = context.VisualPosition(position);
                var renderer = lightTile.AddComponent<SpriteRenderer>();
                renderer.sprite = visuals.GetLightSprite(kind, strength);
                renderer.sortingOrder = context.Iso.SortingOrder(position, -1);
                registry.RegisterLight(renderer, position);
            }
        }

        private static void CreateProp(
            string objectName,
            HubPropKind kind,
            GridPos position,
            HubWorldPresentationContext context,
            HubWorldVisuals visuals,
            HubWorldRegistry registry,
            string animationKey = null,
            HubInteractionTarget? interaction = null)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(context.Root, false);
            root.transform.position = context.VisualPosition(position);
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = visuals.GetPropSprite(kind);
            renderer.sortingOrder = context.Iso.SortingOrder(position, 1);

            if (!string.IsNullOrEmpty(animationKey))
            {
                context.AttachAnimation(
                    root,
                    renderer,
                    visuals.GetAnimationSet(animationKey));
            }

            registry.RegisterProp(renderer, position);
            if (interaction.HasValue)
            {
                HubInteractionTarget target = interaction.Value;
                registry.RegisterInteraction(position, target.Id, target.Label);
            }
        }
    }
}
