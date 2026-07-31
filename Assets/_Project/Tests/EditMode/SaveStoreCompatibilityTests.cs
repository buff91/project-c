using System;
using System.IO;
using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;

namespace ProjectC.Tests
{
    /// <summary>
    /// 다운그레이드 실행이 미래 버전 JSON의 알 수 없는 필드를 지우지 않는지 파일 단위로 검증한다.
    /// 마이그레이션의 버전 번호 보존만 확인해서는 JsonUtility 재직렬화 유실을 잡을 수 없다.
    /// </summary>
    public sealed class SaveStoreCompatibilityTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "project-c-save-compatibility-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        [Test]
        public void RunStore_FutureRootSchemaCannotResumeOrBeOverwritten()
        {
            string path = Path.Combine(_directory, "run.json");
            string original =
                $"{{\"schemaVersion\":{SaveMigration.CurrentVersion + 1}," +
                "\"dungeonId\":\"future-dungeon\",\"futureRoot\":{\"chargeMode\":\"burst\"}}";
            File.WriteAllText(path, original);

            Assert.IsFalse(RunSaveStore.TryLoad(path, out RunSaveData loaded));
            Assert.IsNull(loaded);
            Assert.IsFalse(RunSaveStore.Save(
                path,
                new RunSaveData { dungeonId = DungeonCatalog.DefaultId }));
            Assert.AreEqual(original, File.ReadAllText(path),
                "현재 타입으로 다시 직렬화하면 futureRoot가 조용히 사라진다");

            AtomicJsonStore.Clear(path);
            Assert.IsTrue(RunSaveStore.Save(
                path,
                new RunSaveData { dungeonId = DungeonCatalog.DefaultId }),
                "명시적으로 기존 체크포인트를 지운 뒤에는 새 원정을 저장할 수 있어야 한다");
        }

        [Test]
        public void RunStore_FutureNestedTelemetryCannotBeOverwritten()
        {
            string path = Path.Combine(_directory, "run.json");
            string original =
                $"{{\"schemaVersion\":{SaveMigration.CurrentVersion}," +
                $"\"telemetry\":{{\"schemaVersion\":{RunTelemetry.CurrentSchemaVersion + 1}," +
                "\"futureMetric\":42}}";
            File.WriteAllText(path, original);

            Assert.IsFalse(RunSaveStore.TryLoad(path, out RunSaveData loaded));
            Assert.IsNull(loaded);
            Assert.IsFalse(RunSaveStore.Save(path, new RunSaveData()));
            Assert.AreEqual(original, File.ReadAllText(path),
                "루트가 현재 버전이어도 미래 텔레메트리 필드를 잃으면 안 된다");
        }

        [Test]
        public void RunStore_CompatiblePrimaryDoesNotOverwriteFutureBackup()
        {
            string path = Path.Combine(_directory, "run.json");
            string current =
                $"{{\"schemaVersion\":{SaveMigration.CurrentVersion}," +
                "\"dungeonId\":\"current-dungeon\"}";
            string futureBackup =
                $"{{\"schemaVersion\":{SaveMigration.CurrentVersion + 1}," +
                "\"futureRoot\":{\"chargeMode\":\"burst\"}}";
            File.WriteAllText(path, current);
            File.WriteAllText(AtomicJsonStore.BackupPathFor(path), futureBackup);

            Assert.IsFalse(RunSaveStore.TryLoad(path, out RunSaveData loaded));
            Assert.IsNull(loaded);
            Assert.IsFalse(RunSaveStore.Save(path, new RunSaveData()));
            Assert.AreEqual(current, File.ReadAllText(path));
            Assert.AreEqual(
                futureBackup,
                File.ReadAllText(AtomicJsonStore.BackupPathFor(path)),
                "현재 주 파일을 다시 쓰며 더 새로운 백업을 덮어쓰면 복구할 원본이 사라진다");
        }

        [Test]
        public void MetaStore_FutureSchemaRemainsByteForByteUnchanged()
        {
            string path = Path.Combine(_directory, "meta.json");
            string original =
                $"{{\"schemaVersion\":{SaveMigration.CurrentVersion + 1}," +
                "\"gold\":77,\"futureWallet\":{\"shards\":9}}";
            File.WriteAllText(path, original);

            MetaSaveData loaded = MetaStore.LoadOrNew(path);
            Assert.AreEqual(77, loaded.gold);
            Assert.AreEqual(SaveMigration.CurrentVersion + 1, loaded.schemaVersion);
            Assert.IsFalse(MetaStore.IsWriteCompatible(path));

            Assert.IsFalse(MetaStore.Save(
                path,
                new MetaSaveData { gold = 0 }),
                "미래 파일을 읽은 객체가 아니어도 같은 경로를 덮어쓰면 안 된다");
            Assert.AreEqual(original, File.ReadAllText(path),
                "현재 타입으로 다시 직렬화하면 futureWallet이 조용히 사라진다");
        }

        [Test]
        public void MetaStore_CompatiblePrimaryDoesNotOverwriteFutureBackup()
        {
            string path = Path.Combine(_directory, "meta.json");
            string current =
                $"{{\"schemaVersion\":{SaveMigration.CurrentVersion},\"gold\":10}}";
            string futureBackup =
                $"{{\"schemaVersion\":{SaveMigration.CurrentVersion + 1}," +
                "\"futureWallet\":{\"shards\":9}}";
            File.WriteAllText(path, current);
            File.WriteAllText(AtomicJsonStore.BackupPathFor(path), futureBackup);

            Assert.IsFalse(MetaStore.IsWriteCompatible(path));
            Assert.IsFalse(MetaStore.Save(path, new MetaSaveData { gold = 20 }));
            Assert.AreEqual(current, File.ReadAllText(path));
            Assert.AreEqual(
                futureBackup,
                File.ReadAllText(AtomicJsonStore.BackupPathFor(path)));
        }
    }
}
