using System;
using System.IO;
using NUnit.Framework;
using ProjectC.Gameplay;

namespace ProjectC.Tests
{
    public class DevelopmentSaveProfileTests
    {
        private const string PersistentRoot = "/project-c/persistent";

        [Test]
        public void ResolveRoot_DefaultProfile_UsesPersistentRootDirectly()
        {
            string result = DevelopmentSaveProfile.ResolveRoot(
                PersistentRoot,
                useDevelopmentProfile: false);

            Assert.AreEqual(PersistentRoot, result);
        }

        [Test]
        public void ResolveRoot_DevelopmentProfile_UsesDedicatedChildDirectory()
        {
            string result = DevelopmentSaveProfile.ResolveRoot(
                PersistentRoot,
                useDevelopmentProfile: true);

            Assert.AreEqual(
                Path.Combine(PersistentRoot, DevelopmentSaveProfile.DirectoryName),
                result);
            Assert.AreNotEqual(PersistentRoot, result);
        }

        [Test]
        public void ResolveFile_SameName_DoesNotCollideAcrossProfiles()
        {
            string real = DevelopmentSaveProfile.ResolveFile(
                PersistentRoot,
                useDevelopmentProfile: false,
                DevelopmentSaveProfile.MetaFileName);
            string development = DevelopmentSaveProfile.ResolveFile(
                PersistentRoot,
                useDevelopmentProfile: true,
                DevelopmentSaveProfile.MetaFileName);

            Assert.AreNotEqual(real, development);
            Assert.AreEqual(
                Path.Combine(
                    PersistentRoot,
                    DevelopmentSaveProfile.DirectoryName,
                    DevelopmentSaveProfile.MetaFileName),
                development);
        }

        [TestCase("")]
        [TestCase("../meta-stash.json")]
        [TestCase("nested/run-save.json")]
        public void ResolveFile_RejectsEmptyOrNestedFileName(string fileName)
        {
            Assert.Throws<ArgumentException>(() =>
                DevelopmentSaveProfile.ResolveFile(
                    PersistentRoot,
                    useDevelopmentProfile: true,
                    fileName));
        }
    }
}
