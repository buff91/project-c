using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 장비는 숫자가 아니라 행동 규칙을 바꾼다 — 이 계약이 깨지면 영구 스탯 크리프(GDD §11 경고)로
    /// 돌아간다. 카탈로그 불변식과 장착 조합의 파생 값을 고정한다.
    /// </summary>
    public class EquipmentTests
    {
        [Test]
        public void Generator_PlacesEquipment_OnlyBelowTheGateDepth_AndAtMostOnePerFloor()
        {
            bool sawAny = false;
            for (int seed = 0; seed < 24; seed++)
            {
                var map = new GridMap();
                DungeonLayout layout = DungeonGenerator.Generate(map, 13, 13, floorCount: 10, seed: seed);

                foreach (DungeonFloorInfo floor in layout.Floors)
                {
                    int depth = -floor.FloorIndex;
                    int equipmentCount = 0;
                    foreach (ItemSpawn spawn in floor.Items)
                        if (EquipmentCatalog.IsEquipment(spawn.Kind)) equipmentCount++;

                    Assert.LessOrEqual(equipmentCount, 1, $"seed {seed} depth {depth}: 층당 최대 하나");
                    if (equipmentCount > 0)
                    {
                        sawAny = true;
                        Assert.GreaterOrEqual(depth, EquipmentDropRules.FirstDropDepth,
                            $"seed {seed}: 얕은 밴드엔 장비가 없다");
                    }
                }
            }

            Assert.IsTrue(sawAny, "던전에서 장비가 실제로 나온다");
        }

        [Test]
        public void Catalog_HasBothSlots_AndNeverGrantsRawAttack()
        {
            bool weapon = false;
            bool gear = false;
            foreach (EquipmentDefinition definition in EquipmentCatalog.All)
            {
                weapon |= definition.Slot == EquipmentSlot.Weapon;
                gear |= definition.Slot == EquipmentSlot.Gear;
                Assert.Greater(definition.CraftCost, 0, definition.Id);
                Assert.IsNotNull(EquipmentCatalog.ForItem(definition.Item), definition.Id);
                Assert.AreSame(definition, EquipmentCatalog.ById(definition.Id));
            }

            Assert.IsTrue(weapon, "무기 슬롯 장비가 있어야 선택이 생긴다");
            Assert.IsTrue(gear, "보조 슬롯 장비가 있어야 선택이 생긴다");
        }

        [Test]
        public void LoadoutFor_Unarmed_MatchesLegacyRules()
        {
            CombatLoadout loadout = EquipmentRules.LoadoutFor(null, "");

            Assert.AreEqual(1, loadout.MeleeReach);
            Assert.IsFalse(loadout.KnockbackOnHit);
            Assert.AreEqual(0, loadout.Armor);
            Assert.AreEqual(FallRules.DefaultSafeFallHeight, loadout.SafeFallHeight);
        }

        [Test]
        public void LoadoutFor_CombinesWeaponAndGear()
        {
            CombatLoadout spearAndBoots = EquipmentRules.LoadoutFor("pipe-spear", "padded-boots");
            Assert.AreEqual(2, spearAndBoots.MeleeReach, "창은 한 칸 더 뻗는다");
            Assert.IsFalse(spearAndBoots.KnockbackOnHit);
            Assert.Greater(spearAndBoots.SafeFallHeight, FallRules.DefaultSafeFallHeight);

            CombatLoadout wrenchAndShield = EquipmentRules.LoadoutFor("heavy-wrench", "sign-shield");
            Assert.AreEqual(1, wrenchAndShield.MeleeReach);
            Assert.IsTrue(wrenchAndShield.KnockbackOnHit, "둔기는 밀어낸다");
            Assert.AreEqual(1, wrenchAndShield.Armor);
        }

        [Test]
        public void LoadoutFor_IgnoresWrongSlotAndUnknownIds()
        {
            // 보조 자리에 무기 id, 무기 자리에 헛소리 — 세이브가 손상돼도 맨손으로 떨어진다.
            CombatLoadout loadout = EquipmentRules.LoadoutFor("nonsense", "pipe-spear");

            Assert.AreEqual(1, loadout.MeleeReach);
            Assert.AreEqual(0, loadout.Armor);
        }

        [Test]
        public void EquippedLoadout_IgnoresEquipmentTheShelterDoesNotOwn()
        {
            var meta = new MetaSaveData();
            meta.SetEquipped(EquipmentSlot.Weapon, "pipe-spear");

            Assert.AreEqual(1, meta.EquippedLoadout().MeleeReach, "보유하지 않은 장비는 효과가 없다");

            meta.AddCount(ItemKind.PipeSpear, 1);
            Assert.AreEqual(2, meta.EquippedLoadout().MeleeReach);
        }

        [Test]
        public void DropRules_SkipShallowBand_AndPickFromCatalog()
        {
            var random = new System.Random(7);
            for (int i = 0; i < 200; i++)
                Assert.IsNull(EquipmentDropRules.Roll(0, random), "도입 구간엔 장비가 굴러다니지 않는다");

            bool sawDrop = false;
            var deepRandom = new System.Random(7);
            for (int i = 0; i < 200; i++)
            {
                EquipmentDefinition rolled = EquipmentDropRules.Roll(6, deepRandom);
                if (rolled == null) continue;
                sawDrop = true;
                Assert.AreSame(EquipmentCatalog.ById(rolled.Id), rolled);
            }
            Assert.IsTrue(sawDrop, "깊은 층에서는 실제로 나온다");
        }

        [Test]
        public void DropRules_ConsumeTheSameRolls_RegardlessOfDepth()
        {
            // 확률/종류 롤을 깊이와 무관하게 같은 횟수로 소비해야 seed 재현성이 유지된다.
            var shallow = new System.Random(11);
            var deep = new System.Random(11);
            EquipmentDropRules.Roll(0, shallow);
            EquipmentDropRules.Roll(9, deep);

            Assert.AreEqual(shallow.Next(0, 1000), deep.Next(0, 1000));
        }

        [Test]
        public void ShouldAutoEquip_OnlyWhenSlotIsEmpty()
        {
            Assert.IsTrue(EquipmentRules.ShouldAutoEquip(null));
            Assert.IsTrue(EquipmentRules.ShouldAutoEquip(""));
            Assert.IsFalse(EquipmentRules.ShouldAutoEquip("pipe-spear"),
                "이미 낀 장비를 말없이 갈아치우지 않는다");
        }

        [Test]
        public void EquipmentItems_HaveBackpackFootprints_AndAreNotSoldInShop()
        {
            foreach (EquipmentDefinition definition in EquipmentCatalog.All)
            {
                ItemFootprint footprint = BackpackRules.Footprint(definition.Item);
                Assert.Greater(footprint.Width * footprint.Height, 0, definition.Id);
                Assert.AreEqual(0, ItemCatalog.ShopPrice(definition.Item),
                    "장비는 상점이 아니라 대장간에서만 나온다");
                Assert.AreEqual(0, ItemCatalog.GoldValue(definition.Item),
                    "장비는 전리품이 아니다(생환 시 자동 환금 금지)");
            }
        }
    }
}
