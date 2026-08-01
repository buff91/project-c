using System;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    internal readonly struct HubInteractionTarget
    {
        public string Id { get; }
        public string Label { get; }

        public HubInteractionTarget(string id, string label)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("허브 상호작용 id가 비어 있다.", nameof(id));
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("허브 상호작용 라벨이 비어 있다.", nameof(label));

            Id = id;
            Label = label;
        }
    }

    /// <summary>
    /// 허브 월드의 상호작용 좌표와 지속 프롭/조명 비주얼을 소유한다.
    /// <para>
    /// 이전에는 세 Dictionary가 <see cref="IsoPrototypeDemo"/> 본체에 놓여 Hub·Interaction·View
    /// 파셜이 같은 가변 상태를 직접 공유했다. 이 레지스트리가 등록·초기화·재투영을 한 경계로
    /// 묶어, 허브 빌더와 던전 런타임의 상태 소유권을 분리하는 첫 단계가 된다.
    /// </para>
    /// </summary>
    internal sealed class HubWorldRegistry
    {
        private readonly Dictionary<GridPos, HubInteractionTarget> _interactions =
            new Dictionary<GridPos, HubInteractionTarget>();
        private readonly List<VisualAnchor> _visuals = new List<VisualAnchor>();

        /// <summary>
        /// 등록 참조만 잊는다. GameObject 수명은 기존 Generated Visuals 루트가 소유한다.
        /// </summary>
        public void Reset()
        {
            _interactions.Clear();
            _visuals.Clear();
        }

        public void RegisterInteraction(GridPos position, string id, string label) =>
            _interactions[position] = new HubInteractionTarget(id, label);

        public bool TryGetInteraction(
            GridPos position,
            out HubInteractionTarget target) =>
            _interactions.TryGetValue(position, out target);

        public void RegisterProp(SpriteRenderer renderer, GridPos position) =>
            RegisterVisual(renderer, position, microOffset: 1);

        public void RegisterLight(SpriteRenderer renderer, GridPos position) =>
            RegisterVisual(renderer, position, microOffset: -1);

        /// <summary>
        /// 시점 회전 뒤 허브 비주얼을 같은 격자 좌표로 다시 투영한다. 위치 계산은
        /// <see cref="IsoPrototypeDemo"/>의 지속 비주얼 계약(VisualPosition)을 주입받고,
        /// 정렬은 프로젝트 SSOT인 <see cref="IsoGrid"/>만 사용한다.
        /// </summary>
        public void ApplyView(
            IsoGrid iso,
            Func<GridPos, Vector3> visualPosition)
        {
            if (iso == null) throw new ArgumentNullException(nameof(iso));
            if (visualPosition == null)
                throw new ArgumentNullException(nameof(visualPosition));

            foreach (VisualAnchor anchor in _visuals)
            {
                SpriteRenderer renderer = anchor.Renderer;
                if (renderer == null) continue;
                renderer.transform.position = visualPosition(anchor.Position);
                renderer.sortingOrder = iso.SortingOrder(
                    anchor.Position,
                    anchor.MicroOffset);
            }
        }

        private void RegisterVisual(
            SpriteRenderer renderer,
            GridPos position,
            int microOffset)
        {
            if (renderer == null)
                throw new ArgumentNullException(nameof(renderer));

            for (int i = 0; i < _visuals.Count; i++)
            {
                if (_visuals[i].Renderer != renderer) continue;
                _visuals[i] = new VisualAnchor(renderer, position, microOffset);
                return;
            }

            _visuals.Add(new VisualAnchor(renderer, position, microOffset));
        }

        private readonly struct VisualAnchor
        {
            public SpriteRenderer Renderer { get; }
            public GridPos Position { get; }
            public int MicroOffset { get; }

            public VisualAnchor(
                SpriteRenderer renderer,
                GridPos position,
                int microOffset)
            {
                Renderer = renderer;
                Position = position;
                MicroOffset = microOffset;
            }
        }
    }
}
