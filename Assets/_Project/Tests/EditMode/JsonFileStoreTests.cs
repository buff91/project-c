using System;
using System.IO;
using NUnit.Framework;
using ProjectC.Gameplay;

namespace ProjectC.Tests
{
    public class JsonFileStoreTests
    {
        [Serializable]
        private sealed class SampleData
        {
            public int value;
        }

        private string _directory;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "project-c-json-store-" + Guid.NewGuid());
            _path = Path.Combine(_directory, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void Save_FirstWriteCreatesPrimaryWithoutTemporaryFile()
        {
            JsonFileStore.Save(_path, new SampleData { value = 7 });

            Assert.IsTrue(File.Exists(_path));
            Assert.IsFalse(File.Exists(JsonFileStore.TemporaryPath(_path)));
            Assert.IsTrue(JsonFileStore.TryLoad(
                _path, out SampleData loaded, out bool recovered));
            Assert.AreEqual(7, loaded.value);
            Assert.IsFalse(recovered);
        }

        [Test]
        public void Save_SecondWriteKeepsPreviousPrimaryAsBackup()
        {
            JsonFileStore.Save(_path, new SampleData { value = 3 });
            JsonFileStore.Save(_path, new SampleData { value = 9 });

            Assert.IsTrue(JsonFileStore.TryLoad(
                _path, out SampleData current, out bool currentRecovered));
            Assert.AreEqual(9, current.value);
            Assert.IsFalse(currentRecovered);

            Assert.IsTrue(JsonFileStore.TryLoad(
                JsonFileStore.BackupPath(_path), out SampleData backup, out bool backupRecovered));
            Assert.AreEqual(3, backup.value);
            Assert.IsFalse(backupRecovered);
        }

        [Test]
        public void TryLoad_CorruptPrimaryRecoversBackupAndRepairsPrimary()
        {
            JsonFileStore.Save(_path, new SampleData { value = 4 });
            JsonFileStore.Save(_path, new SampleData { value = 8 });
            File.WriteAllText(_path, "{ broken json");

            Assert.IsTrue(JsonFileStore.TryLoad(
                _path, out SampleData recovered, out bool recoveredFromBackup));
            Assert.AreEqual(4, recovered.value);
            Assert.IsTrue(recoveredFromBackup);

            Assert.IsTrue(JsonFileStore.TryLoad(
                _path, out SampleData repaired, out bool recoveredAgain));
            Assert.AreEqual(4, repaired.value);
            Assert.IsFalse(recoveredAgain);
        }

        [Test]
        public void Exists_BackupAloneCountsAsRecoverableSave()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(JsonFileStore.BackupPath(_path), "{\"value\":11}");

            Assert.IsTrue(JsonFileStore.Exists(_path));
            Assert.IsTrue(JsonFileStore.TryLoad(
                _path, out SampleData recovered, out bool recoveredFromBackup));
            Assert.AreEqual(11, recovered.value);
            Assert.IsTrue(recoveredFromBackup);
            Assert.IsTrue(File.Exists(_path));
        }

        [Test]
        public void Clear_RemovesPrimaryBackupAndTemporaryFiles()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "primary");
            File.WriteAllText(JsonFileStore.BackupPath(_path), "backup");
            File.WriteAllText(JsonFileStore.TemporaryPath(_path), "temporary");

            JsonFileStore.Clear(_path);

            Assert.IsFalse(JsonFileStore.Exists(_path));
            Assert.IsFalse(File.Exists(JsonFileStore.TemporaryPath(_path)));
        }
    }
}
