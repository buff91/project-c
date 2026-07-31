using System;
using System.IO;
using NUnit.Framework;
using ProjectC.Gameplay;

namespace ProjectC.Tests
{
    public class AtomicJsonStoreTests
    {
        [Serializable]
        private sealed class TestData
        {
            public int value;
            public string label;
        }

        private string _directory;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "project-c-atomic-json-tests",
                Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void SaveAndLoad_RoundTripsData()
        {
            AtomicJsonStore.Save(_path, new TestData { value = 7, label = "primary" });

            bool loaded = AtomicJsonStore.TryLoad(
                _path,
                out TestData data,
                out bool recovered,
                out string serializedData);

            Assert.IsTrue(loaded);
            Assert.IsFalse(recovered);
            Assert.AreEqual(7, data.value);
            Assert.AreEqual("primary", data.label);
            StringAssert.Contains("\"value\":7", serializedData);
            StringAssert.Contains("\"label\":\"primary\"", serializedData);
            Assert.IsFalse(File.Exists(AtomicJsonStore.TemporaryPathFor(_path)));
        }

        [Test]
        public void SecondSave_PreservesPreviousDataAsBackup()
        {
            AtomicJsonStore.Save(_path, new TestData { value = 1, label = "previous" });
            AtomicJsonStore.Save(_path, new TestData { value = 2, label = "current" });

            bool loaded = AtomicJsonStore.TryLoad(
                AtomicJsonStore.BackupPathFor(_path),
                out TestData backup,
                out bool recovered);

            Assert.IsTrue(loaded);
            Assert.IsFalse(recovered);
            Assert.AreEqual(1, backup.value);
            Assert.AreEqual("previous", backup.label);
        }

        [Test]
        public void CorruptPrimary_RecoversBackupAndRestoresPrimary()
        {
            AtomicJsonStore.Save(_path, new TestData { value = 1, label = "safe" });
            AtomicJsonStore.Save(_path, new TestData { value = 2, label = "latest" });
            File.WriteAllText(_path, "{ broken json");

            bool loaded = AtomicJsonStore.TryLoad(
                _path,
                out TestData recoveredData,
                out bool recovered,
                out string serializedData);

            Assert.IsTrue(loaded);
            Assert.IsTrue(recovered);
            Assert.AreEqual(1, recoveredData.value);
            Assert.AreEqual("safe", recoveredData.label);
            StringAssert.Contains("\"label\":\"safe\"", serializedData,
                "복구 때도 손상된 주 파일이 아니라 실제로 읽은 백업 원문을 돌려줘야 한다");

            Assert.IsTrue(AtomicJsonStore.TryLoad(
                _path,
                out TestData restoredPrimary,
                out bool recoveredAgain));
            Assert.IsFalse(recoveredAgain);
            Assert.AreEqual(1, restoredPrimary.value);
        }

        [Test]
        public void MissingPrimary_RecoversExistingBackup()
        {
            AtomicJsonStore.Save(_path, new TestData { value = 3, label = "backup" });
            File.Move(_path, AtomicJsonStore.BackupPathFor(_path));

            Assert.IsTrue(AtomicJsonStore.HasSave(_path));
            Assert.IsTrue(AtomicJsonStore.TryLoad(
                _path,
                out TestData data,
                out bool recovered));
            Assert.IsTrue(recovered);
            Assert.AreEqual(3, data.value);
            Assert.IsTrue(File.Exists(_path));
        }

        [Test]
        public void Clear_RemovesPrimaryBackupAndTemporaryFiles()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "primary");
            File.WriteAllText(AtomicJsonStore.BackupPathFor(_path), "backup");
            File.WriteAllText(AtomicJsonStore.TemporaryPathFor(_path), "temporary");

            AtomicJsonStore.Clear(_path);

            Assert.IsFalse(AtomicJsonStore.HasSave(_path));
            Assert.IsFalse(File.Exists(AtomicJsonStore.TemporaryPathFor(_path)));
        }
    }
}
