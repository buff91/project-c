using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 대장간: 골드로 장비를 만들고 슬롯에 끼운다. 옛 영구 스탯 강화를 대체하므로
    /// "판을 거듭해도 공격력이 오르지 않는다"가 이 규칙의 존재 이유다.
    /// </summary>
    public class ForgeRulesTests
    {
        private static MetaSaveData Rich(int gold = 500) => new MetaSaveData { gold = gold };

        [Test]
        public void TryCraft_SpendsGold_StoresEquipment_AndFillsEmptySlot()
        {
            MetaSaveData meta = Rich();
            EquipmentDefinition spear = EquipmentCatalog.ById("pipe-spear");

            Assert.AreEqual(ForgeResult.Crafted, ForgeRules.TryCraft(meta, "pipe-spear"));

            Assert.AreEqual(500 - spear.CraftCost, meta.gold);
            Assert.AreEqual(1, meta.GetCount(ItemKind.PipeSpear));
            Assert.IsTrue(ForgeRules.IsEquipped(meta, spear), "빈 슬롯이면 만든 즉시 장착한다");
            Assert.AreEqual(2, meta.EquippedLoadout().MeleeReach);
        }

        [Test]
        public void TryCraft_RejectsDuplicateAndPoverty()
        {
            MetaSaveData meta = Rich();
            ForgeRules.TryCraft(meta, "pipe-spear");

            Assert.AreEqual(ForgeResult.AlreadyOwned, ForgeRules.TryCraft(meta, "pipe-spear"));
            Assert.AreEqual(ForgeResult.UnknownEquipment, ForgeRules.TryCraft(meta, "nonsense"));

            var broke = new MetaSaveData { gold = 1 };
            Assert.AreEqual(ForgeResult.InsufficientGold, ForgeRules.TryCraft(broke, "pipe-spear"));
            Assert.AreEqual(1, broke.gold, "실패한 제작은 골드를 쓰지 않는다");
            Assert.AreEqual(0, broke.GetCount(ItemKind.PipeSpear));
        }

        [Test]
        public void SecondWeapon_DoesNotAutoReplaceTheEquippedOne()
        {
            MetaSaveData meta = Rich();
            ForgeRules.TryCraft(meta, "pipe-spear");
            ForgeRules.TryCraft(meta, "heavy-wrench");

            Assert.IsTrue(ForgeRules.IsEquipped(meta, EquipmentCatalog.ById("pipe-spear")),
                "이미 낀 무기를 새 제작이 말없이 밀어내지 않는다");
            Assert.IsFalse(ForgeRules.IsEquipped(meta, EquipmentCatalog.ById("heavy-wrench")));
        }

        [Test]
        public void TryToggleEquip_SwapsWithinSlot_AndUnequipsOnRepeat()
        {
            MetaSaveData meta = Rich();
            ForgeRules.TryCraft(meta, "pipe-spear");
            ForgeRules.TryCraft(meta, "heavy-wrench");

            Assert.IsTrue(ForgeRules.TryToggleEquip(meta, "heavy-wrench"));
            Assert.IsTrue(meta.EquippedLoadout().KnockbackOnHit, "같은 슬롯이면 교체된다");
            Assert.AreEqual(1, meta.EquippedLoadout().MeleeReach);

            Assert.IsTrue(ForgeRules.TryToggleEquip(meta, "heavy-wrench"));
            Assert.AreEqual("", meta.GetEquipped(EquipmentSlot.Weapon), "다시 고르면 해제된다");
        }

        [Test]
        public void TryToggleEquip_RejectsUnownedEquipment()
        {
            MetaSaveData meta = Rich();

            Assert.IsFalse(ForgeRules.TryToggleEquip(meta, "sign-shield"));
            Assert.AreEqual("", meta.GetEquipped(EquipmentSlot.Gear));
        }

        [Test]
        public void WeaponAndGear_OccupyIndependentSlots()
        {
            MetaSaveData meta = Rich();
            ForgeRules.TryCraft(meta, "pipe-spear");
            ForgeRules.TryCraft(meta, "sign-shield");

            CombatLoadout loadout = meta.EquippedLoadout();
            Assert.AreEqual(2, loadout.MeleeReach);
            Assert.AreEqual(1, loadout.Armor);
        }
    }
}
