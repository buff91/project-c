using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>백팩 격자에서 아이템 하나가 차지하는 고정 크기. 회전은 허용하지 않는다.</summary>
    public readonly struct ItemFootprint : IEquatable<ItemFootprint>
    {
        public readonly int Width;
        public readonly int Height;

        public int Area => Width * Height;

        public ItemFootprint(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
        }

        public bool Equals(ItemFootprint other) => Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is ItemFootprint other && Equals(other);
        public override int GetHashCode() => (Width * 397) ^ Height;
        public override string ToString() => $"{Width}×{Height}";
    }

    /// <summary>자동 정리가 결정한 아이템 인스턴스 하나의 백팩 위치.</summary>
    public readonly struct BackpackPlacement
    {
        public readonly ItemKind Kind;
        public readonly int InstanceIndex;
        public readonly int X;
        public readonly int Y;
        public readonly ItemFootprint Footprint;

        public BackpackPlacement(
            ItemKind kind,
            int instanceIndex,
            int x,
            int y,
            ItemFootprint footprint)
        {
            Kind = kind;
            InstanceIndex = instanceIndex;
            X = x;
            Y = y;
            Footprint = footprint;
        }
    }

    /// <summary>UI와 용량 판정이 함께 사용하는 불변 자동 배치 결과.</summary>
    public sealed class BackpackLayout
    {
        private readonly List<BackpackPlacement> _placements;

        public int Columns { get; }
        public int Rows { get; }
        public int UsedCells { get; }
        public int Capacity => Columns * Rows;
        public IReadOnlyList<BackpackPlacement> Placements => _placements;

        internal BackpackLayout(
            int columns,
            int rows,
            int usedCells,
            List<BackpackPlacement> placements)
        {
            Columns = columns;
            Rows = rows;
            UsedCells = usedCells;
            _placements = placements ?? throw new ArgumentNullException(nameof(placements));
        }
    }

    /// <summary>
    /// 백팩 압박 게이지의 규칙. UI가 자체 판정하지 않도록 Core가 소유한다
    /// (같은 이유로 <c>ItemUnlockRules</c>도 판정이 한 곳뿐이다).
    ///
    /// <para>
    /// 이 게이지가 재는 것은 <b>내구도가 아니라 점유율</b>이다. 계획에는 "내구도/충전 게이지"라고
    /// 적혀 있었지만 <see cref="ItemDefinition"/>에 내구도도 충전도 없다 — 없는 값을 눈금으로
    /// 그리면 화면이 시스템에 없는 규칙을 약속하게 된다. 내구도를 넣으려면 마모·파손·수리(대장간)
    /// ·세이브 포맷이 함께 정해져야 하고, 그건 GDD/SYSTEMS가 먼저 결정할 일이지 UI 작업이
    /// 곁다리로 만들 것이 아니다.
    /// </para>
    /// <para>
    /// 점유율을 고른 이유: 백팩 칸은 플레이어가 <b>매 판 실제로 관리하는</b> 유일한 수치이고
    /// (기둥 ④ 파밍 &amp; 조합), "주울 자리가 남았나"는 생환 판돈과 직결된다. 숫자("18 / 24 칸")는
    /// 이미 있었지만 압박은 한눈에 안 읽혔다 — 막대가 하는 일이 그것이다.
    /// </para>
    /// </summary>
    public static class BackpackPressure
    {
        /// <summary>이 비율 이상이면 경고색으로 바뀐다 — "이제 곧 못 줍는다".</summary>
        public const float WarningRatio = 0.8f;

        /// <summary>0~1로 정규화한 점유율. 용량이 0이면 0(게이지를 그리지 않는다).</summary>
        public static float Ratio(int usedCells, int capacity)
        {
            if (capacity <= 0 || usedCells <= 0) return 0f;
            float ratio = usedCells / (float)capacity;
            return ratio > 1f ? 1f : ratio;
        }

        /// <summary>경고 구간인가. 가득 찬 경우도 포함한다.</summary>
        public static bool IsWarning(int usedCells, int capacity) =>
            capacity > 0 && Ratio(usedCells, capacity) >= WarningRatio;
    }

    /// <summary>
    /// 디아블로식 멀티슬롯 백팩 규칙.
    /// 큰 아이템부터 행 우선으로 다시 정리해 모바일에서도 드래그 없이 항상 같은 배치를 만든다.
    /// </summary>
    public static class BackpackRules
    {
        public const int Columns = 6;
        public const int Rows = 4;
        public const int Capacity = Columns * Rows;

        /// <summary>아이템 정의에 포함된 백팩 크기. 기존 호출부 호환용 위임 API.</summary>
        public static ItemFootprint Footprint(ItemKind kind) => ItemCatalog.For(kind).Footprint;

        /// <summary>
        /// 현재 수량을 고정 격자에 모두 놓는다. 단 하나라도 못 놓으면 false이며 부분 결과를 노출하지 않는다.
        /// </summary>
        public static bool TryCreateLayout(
            Inventory inventory,
            int columns,
            int rows,
            out BackpackLayout layout)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
            if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));

            var entries = new List<PackingEntry>();
            int usedCells = 0;
            for (int kindOrder = 0; kindOrder < ItemCatalog.AllKinds.Length; kindOrder++)
            {
                ItemKind kind = ItemCatalog.AllKinds[kindOrder];
                int count = inventory.Count(kind);
                ItemFootprint footprint = Footprint(kind);
                for (int instanceIndex = 0; instanceIndex < count; instanceIndex++)
                    entries.Add(new PackingEntry(kind, instanceIndex, kindOrder, footprint));
                usedCells += count * footprint.Area;
            }

            if (usedCells > columns * rows)
            {
                layout = null;
                return false;
            }

            entries.Sort(ComparePackingEntries);
            var occupied = new bool[columns, rows];
            var placements = new List<BackpackPlacement>(entries.Count);
            foreach (PackingEntry entry in entries)
            {
                if (!TryPlace(entry.Footprint, occupied, columns, rows, out int x, out int y))
                {
                    layout = null;
                    return false;
                }

                MarkOccupied(entry.Footprint, occupied, x, y);
                placements.Add(new BackpackPlacement(
                    entry.Kind,
                    entry.InstanceIndex,
                    x,
                    y,
                    entry.Footprint));
            }

            layout = new BackpackLayout(columns, rows, usedCells, placements);
            return true;
        }

        private static int ComparePackingEntries(PackingEntry left, PackingEntry right)
        {
            int byArea = right.Footprint.Area.CompareTo(left.Footprint.Area);
            if (byArea != 0) return byArea;

            int byHeight = right.Footprint.Height.CompareTo(left.Footprint.Height);
            if (byHeight != 0) return byHeight;

            int byKind = left.KindOrder.CompareTo(right.KindOrder);
            return byKind != 0 ? byKind : left.InstanceIndex.CompareTo(right.InstanceIndex);
        }

        private static bool TryPlace(
            ItemFootprint footprint,
            bool[,] occupied,
            int columns,
            int rows,
            out int placedX,
            out int placedY)
        {
            for (int y = 0; y <= rows - footprint.Height; y++)
            for (int x = 0; x <= columns - footprint.Width; x++)
            {
                bool available = true;
                for (int dy = 0; dy < footprint.Height && available; dy++)
                for (int dx = 0; dx < footprint.Width; dx++)
                {
                    if (!occupied[x + dx, y + dy]) continue;
                    available = false;
                    break;
                }

                if (!available) continue;
                placedX = x;
                placedY = y;
                return true;
            }

            placedX = -1;
            placedY = -1;
            return false;
        }

        private static void MarkOccupied(
            ItemFootprint footprint,
            bool[,] occupied,
            int x,
            int y)
        {
            for (int dy = 0; dy < footprint.Height; dy++)
            for (int dx = 0; dx < footprint.Width; dx++)
                occupied[x + dx, y + dy] = true;
        }

        private readonly struct PackingEntry
        {
            public readonly ItemKind Kind;
            public readonly int InstanceIndex;
            public readonly int KindOrder;
            public readonly ItemFootprint Footprint;

            public PackingEntry(
                ItemKind kind,
                int instanceIndex,
                int kindOrder,
                ItemFootprint footprint)
            {
                Kind = kind;
                InstanceIndex = instanceIndex;
                KindOrder = kindOrder;
                Footprint = footprint;
            }
        }
    }
}
