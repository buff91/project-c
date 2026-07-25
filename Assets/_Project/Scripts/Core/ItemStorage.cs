using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 저장 가능한 "종류 → 수량" 한 칸. JsonUtility가 Dictionary를 직렬화하지 못해
    /// 목록 형태로 둔다(그래서 세이브 클래스가 아이템마다 필드를 늘리지 않아도 된다).
    /// </summary>
    [Serializable]
    public sealed class ItemStack
    {
        public ItemKind kind;
        public int count;

        public ItemStack() { }

        public ItemStack(ItemKind kind, int count)
        {
            this.kind = kind;
            this.count = count;
        }
    }

    /// <summary>
    /// 아이템 수량 목록을 다루는 공용 연산. 창고·출정 로드아웃·런 세이브가 같은 규칙을 쓴다.
    ///
    /// **아이템을 새로 추가할 때 손댈 곳을 없애는 것**이 이 파일의 목적이다. 예전에는
    /// 세이브 클래스마다 int 필드와 switch 가 있어서 종류 하나를 늘릴 때 여섯 군데를 고쳐야 했고,
    /// 한 곳만 빠뜨려도 아이템이 조용히 사라졌다. 이제 <see cref="ItemKind"/>에 값을 더하면 끝난다.
    /// </summary>
    public static class ItemStorage
    {
        public static int Count(List<ItemStack> stacks, ItemKind kind)
        {
            if (stacks == null) return 0;
            foreach (ItemStack stack in stacks)
                if (stack != null && stack.kind == kind)
                    return stack.count > 0 ? stack.count : 0;
            return 0;
        }

        /// <summary>수량을 더한다(음수면 뺀다). 0 이하가 된 칸은 목록에서 지워 저장을 깔끔히 유지한다.</summary>
        public static void Add(List<ItemStack> stacks, ItemKind kind, int amount)
        {
            if (stacks == null || amount == 0) return;

            for (int i = 0; i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack == null || stack.kind != kind) continue;

                stack.count += amount;
                if (stack.count <= 0) stacks.RemoveAt(i);
                return;
            }

            if (amount > 0) stacks.Add(new ItemStack(kind, amount));
        }

        /// <summary>보유량을 넘지 않게 빼고 실제 제거량을 반환한다.</summary>
        public static int Remove(List<ItemStack> stacks, ItemKind kind, int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

            int removed = Math.Min(Count(stacks, kind), amount);
            if (removed > 0) Add(stacks, kind, -removed);
            return removed;
        }

        public static void Clear(List<ItemStack> stacks) => stacks?.Clear();

        /// <summary>인벤토리의 모든 종류를 목록으로 옮겨 담는다(기존 내용은 버린다).</summary>
        public static void CopyFrom(List<ItemStack> stacks, Inventory inventory)
        {
            if (stacks == null) return;
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));

            stacks.Clear();
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                int count = inventory.Count(kind);
                if (count > 0) stacks.Add(new ItemStack(kind, count));
            }
        }

        /// <summary>목록의 수량을 인벤토리에 더한다(<see cref="CopyFrom"/>의 역).</summary>
        public static void AddTo(List<ItemStack> stacks, Inventory inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (stacks == null) return;

            foreach (ItemStack stack in stacks)
            {
                if (stack == null || stack.count <= 0) continue;
                inventory.Add(stack.kind, stack.count);
            }
        }
    }
}
