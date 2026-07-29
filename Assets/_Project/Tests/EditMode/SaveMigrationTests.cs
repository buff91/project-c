using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 세이브 스키마 변환. 이 테스트가 지키는 것은 값 하나가 아니라 <b>플레이어의 창고</b>다 —
    /// 변환이 한 번 잘못 돌면 기존 플레이어의 소모품이 통째로 줄거나 늘어난다.
    /// </summary>
    public sealed class SaveMigrationTests
    {
        /// <summary>물약 칸당 2회분이라고 가정한 테스트용 배수(카탈로그 실값과 무관).</summary>
        private static int TestCharges(ItemKind kind) => kind == ItemKind.Potion ? 2 : 1;

        /// <summary>
        /// <b>이 파일에서 가장 중요한 테스트.</b> `schemaVersion`에 이니셜라이저를 붙이면
        /// (예: <c>= 1</c>) JsonUtility가 그 필드 없는 구세이브를 읽을 때 기본값이 남아
        /// **구세이브가 자기를 최신이라고 선언**하고 변환이 통째로 건너뛰어진다.
        /// `RunTelemetry.schemaVersion`이 그 패턴을 쓰고 있어 복사하기 쉽다.
        /// </summary>
        [Test]
        public void SchemaVersion_DefaultsToZeroSoOldSavesAreDetected()
        {
            Assert.AreEqual(0, new MetaSaveData().schemaVersion);
            Assert.AreEqual(0, new RunSaveData().schemaVersion);
        }

        [Test]
        public void Migrate_ScalesStashCountsToCharges()
        {
            var meta = new MetaSaveData();
            meta.AddCount(ItemKind.Potion, 2);

            Assert.IsTrue(SaveMigration.Migrate(meta, TestCharges));

            Assert.AreEqual(4, meta.GetCount(ItemKind.Potion), "물약 2개 = 4회분");
            Assert.AreEqual(SaveMigration.CurrentVersion, meta.schemaVersion);
        }

        [Test]
        public void Migrate_ScalesLoadoutAndRunItemsToo()
        {
            var meta = new MetaSaveData();
            meta.AddLoadoutCount(ItemKind.Potion, 3);
            Assert.IsTrue(SaveMigration.Migrate(meta, TestCharges));
            Assert.AreEqual(6, meta.GetLoadoutCount(ItemKind.Potion));

            var run = new RunSaveData();
            ItemStorage.Add(run.items, ItemKind.Potion, 5);
            Assert.IsTrue(SaveMigration.Migrate(run, TestCharges));
            Assert.AreEqual(10, ItemStorage.Count(run.items, ItemKind.Potion));
        }

        /// <summary>
        /// 충전이 없는 종류는 개수가 곧 수량이라 손대면 안 된다 — 여길 잘못 건드리면
        /// 전리품 정산 골드가 배로 튄다.
        /// </summary>
        [Test]
        public void Migrate_LeavesNonChargedKindsAlone()
        {
            var meta = new MetaSaveData();
            meta.AddCount(ItemKind.Bomb, 4);
            meta.AddLoadoutCount(ItemKind.Herb, 7);

            SaveMigration.Migrate(meta, TestCharges);

            Assert.AreEqual(4, meta.GetCount(ItemKind.Bomb));
            Assert.AreEqual(7, meta.GetLoadoutCount(ItemKind.Herb));
        }

        /// <summary>로드가 여러 번 돌아도 값이 계속 불어나면 안 된다.</summary>
        [Test]
        public void Migrate_IsIdempotent()
        {
            var meta = new MetaSaveData();
            meta.AddCount(ItemKind.Potion, 2);

            Assert.IsTrue(SaveMigration.Migrate(meta, TestCharges), "첫 변환");
            Assert.IsFalse(SaveMigration.Migrate(meta, TestCharges), "두 번째는 no-op");
            Assert.IsFalse(SaveMigration.Migrate(meta, TestCharges), "세 번째도 no-op");

            Assert.AreEqual(4, meta.GetCount(ItemKind.Potion));
        }

        [Test]
        public void Migrate_SkipsSavesAlreadyStamped()
        {
            var meta = new MetaSaveData();
            meta.AddCount(ItemKind.Potion, 2);
            SaveMigration.Stamp(meta);

            Assert.IsFalse(SaveMigration.Migrate(meta, TestCharges));
            Assert.AreEqual(2, meta.GetCount(ItemKind.Potion), "이미 최신이면 안 곱한다");
        }

        /// <summary>
        /// <b>칸당 충전 값을 바꾸는 것은 스키마 변경이 아니다.</b> v0 는 단위가 달라서(개수)
        /// 변환이 필요했지만 v1 이후의 `count`는 이미 충전이고, 칸당 값이 바뀌어도 충전은
        /// 같은 뜻이다 — 달라지는 것은 파생되는 칸수뿐이며 그게 이 기능의 목적이다.
        /// 여기서 배수를 한 번 더 곱하면 보존이 아니라 소지량 증정이 된다.
        /// </summary>
        [Test]
        public void Migrate_DoesNotRescaleWhenAChargeValueChanges()
        {
            var meta = new MetaSaveData();
            meta.AddCount(ItemKind.CannedFood, 5);
            SaveMigration.Stamp(meta); // 이미 충전 단위로 기록된 세이브

            Assert.IsFalse(SaveMigration.Migrate(meta, ItemCatalog.ChargesPerItem));

            Assert.AreEqual(5, meta.GetCount(ItemKind.CannedFood),
                "회분은 그대로다 — 줄어드는 것은 칸수뿐이다");
            Assert.AreEqual(
                ChargeUnits.UnitsFor(ItemKind.CannedFood, 5),
                ChargeUnits.UnitsFor(ItemKind.CannedFood, meta.GetCount(ItemKind.CannedFood)));
        }

        [Test]
        public void Migrate_HandlesEmptySaves()
        {
            var meta = new MetaSaveData();
            var run = new RunSaveData();

            Assert.IsTrue(SaveMigration.Migrate(meta, TestCharges));
            Assert.IsTrue(SaveMigration.Migrate(run, TestCharges));

            Assert.AreEqual(SaveMigration.CurrentVersion, meta.schemaVersion);
            Assert.AreEqual(SaveMigration.CurrentVersion, run.schemaVersion);
        }

        /// <summary>
        /// 프로덕션이 실제로 넘기는 배수(카탈로그)로도 돌아야 한다. 값 자체는 단언하지
        /// 않는다 — 밸런스는 바뀔 수 있고 이 테스트가 소유할 것은 "터지지 않는다"이다.
        /// </summary>
        [Test]
        public void Migrate_WorksWithTheRealCatalog()
        {
            var meta = new MetaSaveData();
            foreach (ItemKind kind in ItemCatalog.AllKinds)
                if (!ItemCatalog.IsTreasure(kind))
                    meta.AddCount(kind, 1);

            Assert.DoesNotThrow(() => SaveMigration.Migrate(meta, ItemCatalog.ChargesPerItem));

            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                if (ItemCatalog.IsTreasure(kind)) continue;
                Assert.AreEqual(
                    ItemCatalog.ChargesPerItem(kind), meta.GetCount(kind), kind.ToString());
            }
        }

        [Test]
        public void Migrate_RejectsNullArguments()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => SaveMigration.Migrate((MetaSaveData)null, TestCharges));
            Assert.Throws<System.ArgumentNullException>(
                () => SaveMigration.Migrate(new MetaSaveData(), null));
            Assert.Throws<System.ArgumentNullException>(
                () => SaveMigration.Migrate((RunSaveData)null, TestCharges));
        }
    }
}
