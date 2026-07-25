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
        public void TakeIntoExpedition_RemovesFromStash_SoDeathCanTakeIt()
        {
            MetaSaveData meta = Rich();
            ForgeRules.TryCraft(meta, "pipe-spear");
            ForgeRules.TryCraft(meta, "sign-shield");

            CombatLoadout carried = ForgeRules.TakeIntoExpedition(
                meta, out string weaponId, out string gearId);

            Assert.AreEqual("pipe-spear", weaponId);
            Assert.AreEqual("sign-shield", gearId);
            Assert.AreEqual(2, carried.MeleeReach, "반입한 장비가 이번 판의 보정을 만든다");
            Assert.AreEqual(0, meta.GetCount(ItemKind.PipeSpear), "들고 나갔으니 창고에는 없다");
            Assert.AreEqual(0, meta.GetCount(ItemKind.SignShield));
        }

        [Test]
        public void DeathLosesCarriedEquipment_ButStashSpareSurvives()
        {
            MetaSaveData meta = Rich();
            ForgeRules.TryCraft(meta, "pipe-spear");
            ForgeRules.TryCraft(meta, "padded-boots");
            // 창고에 예비 무기를 하나 더 둔다(제작이 아니라 직접 적립 — 두 자루째 시나리오).
            meta.AddCount(ItemKind.HeavyWrench, 1);

            ForgeRules.TakeIntoExpedition(meta, out string weaponId, out string gearId);
            ForgeRules.LoseExpeditionEquipment(meta, weaponId, gearId);

            Assert.AreEqual(0, meta.GetCount(ItemKind.PipeSpear), "반입한 장비는 돌아오지 않는다");
            Assert.AreEqual(0, meta.GetCount(ItemKind.PaddedBoots));
            Assert.AreEqual("", meta.GetEquipped(EquipmentSlot.Weapon), "슬롯도 비운다");
            Assert.AreEqual("", meta.GetEquipped(EquipmentSlot.Gear));
            Assert.AreEqual(1, meta.GetCount(ItemKind.HeavyWrench),
                "창고에 남긴 예비 장비는 안전하다");
            Assert.AreEqual(1, meta.EquippedLoadout().MeleeReach, "맨손으로 돌아간다");
        }

        [Test]
        public void ExtractionReturnsCarriedEquipment_StillEquipped()
        {
            MetaSaveData meta = Rich();
            ForgeRules.TryCraft(meta, "heavy-wrench");
            ForgeRules.TryCraft(meta, "sign-shield");

            ForgeRules.TakeIntoExpedition(meta, out string weaponId, out string gearId);
            ForgeRules.ReturnFromExpedition(meta, weaponId, gearId);

            Assert.AreEqual(1, meta.GetCount(ItemKind.HeavyWrench));
            Assert.AreEqual(1, meta.GetCount(ItemKind.SignShield));
            Assert.IsTrue(meta.EquippedLoadout().KnockbackOnHit, "살아 나오면 장착 그대로다");
            Assert.AreEqual(1, meta.EquippedLoadout().Armor);
        }

        [Test]
        public void TakeIntoExpedition_WithNothingEquipped_IsUnarmed()
        {
            MetaSaveData meta = Rich();

            CombatLoadout carried = ForgeRules.TakeIntoExpedition(
                meta, out string weaponId, out string gearId);

            Assert.AreEqual("", weaponId);
            Assert.AreEqual("", gearId);
            Assert.AreEqual(1, carried.MeleeReach);
            Assert.AreEqual(0, carried.Armor);
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
