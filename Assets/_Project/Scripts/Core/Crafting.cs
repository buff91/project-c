using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 조합 레시피 — 재료 2칸 + 산출물 1개 (GDD §5.6).
    /// 산출물은 기존 아이템만 사용해 조합이 새 효과 구현을 요구하지 않게 한다.
    /// </summary>
    public readonly struct Recipe
    {
        public readonly ItemKind IngredientA;
        public readonly ItemKind IngredientB;
        public readonly ItemKind Output;

        public Recipe(ItemKind a, ItemKind b, ItemKind output)
        {
            IngredientA = a;
            IngredientB = b;
            Output = output;
        }

        /// <summary>같은 종류 2개 레시피 여부.</summary>
        public bool IsPair => IngredientA == IngredientB;

        public override string ToString() => $"{IngredientA}+{IngredientB}={Output}";
    }

    /// <summary>
    /// 조합 규칙의 단일 출처. 레시피 목록·판정·실행을 담당한다.
    /// 해금(메타 프로그레션 연동)은 GDD §11 확정 후 여기에 얹는다 — 지금은 전부 해금 상태.
    /// </summary>
    public static class CraftingRules
    {
        public static readonly Recipe[] Recipes =
        {
            new Recipe(ItemKind.Herb, ItemKind.Herb, ItemKind.Potion),
            new Recipe(ItemKind.BlastPowder, ItemKind.BlastPowder, ItemKind.Bomb),
            new Recipe(ItemKind.Bomb, ItemKind.FrostShard, ItemKind.FrostBomb)
        };

        /// <summary>재료 두 개(순서 무관)에 맞는 레시피를 찾는다. 없으면 false.</summary>
        public static bool TryFindRecipe(ItemKind a, ItemKind b, out Recipe recipe)
        {
            foreach (Recipe candidate in Recipes)
            {
                if ((candidate.IngredientA == a && candidate.IngredientB == b) ||
                    (candidate.IngredientA == b && candidate.IngredientB == a))
                {
                    recipe = candidate;
                    return true;
                }
            }
            recipe = default;
            return false;
        }

        /// <summary>인벤토리에 레시피 재료가 충분한지 판정한다.</summary>
        public static bool CanCraft(Inventory inventory, Recipe recipe)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            return recipe.IsPair
                ? inventory.Count(recipe.IngredientA) >= 2
                : inventory.Count(recipe.IngredientA) >= 1 && inventory.Count(recipe.IngredientB) >= 1;
        }

        /// <summary>재료를 소비하고 산출물을 넣는다. 재료가 모자라면 아무것도 바꾸지 않고 false.</summary>
        public static bool TryCraft(Inventory inventory, Recipe recipe)
        {
            if (!CanCraft(inventory, recipe)) return false;
            inventory.TryUse(recipe.IngredientA);
            inventory.TryUse(recipe.IngredientB);
            inventory.Add(recipe.Output);
            return true;
        }

        /// <summary>현재 인벤토리로 만들 수 있는 레시피 목록 (조합 UI 표시용).</summary>
        public static List<Recipe> CraftableRecipes(Inventory inventory)
        {
            var result = new List<Recipe>();
            foreach (Recipe recipe in Recipes)
                if (CanCraft(inventory, recipe)) result.Add(recipe);
            return result;
        }
    }
}
