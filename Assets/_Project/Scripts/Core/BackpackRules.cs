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
    /// 디아블로식 멀티슬롯 백팩 규칙.
    /// 큰 아이템부터 행 우선으로 다시 정리해 모바일에서도 드래그 없이 항상 같은 배치를 만든다.
    /// </summary>
    public static class BackpackRules
    {
        public const int Columns = 6;
        public const int Rows = 4;
        public const int Capacity = Columns * Rows;

        /// <summary>아이템 크기의 단일 출처. 장비가 추가되면 이 매핑만 확장한다.</summary>
        public static ItemFootprint Footprint(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Relic:
                    return new ItemFootprint(2, 2);
                case ItemKind.OilFlask:
                case ItemKind.ThrowingKnife:
                case ItemKind.RecallScroll:
                    return new ItemFootprint(1, 2);
                default:
                    return new ItemFootprint(1, 1);
            }
        }

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
