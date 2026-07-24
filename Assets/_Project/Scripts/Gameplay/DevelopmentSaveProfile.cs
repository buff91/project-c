using System;
using System.IO;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 에디터/개발 빌드에서 실제 플레이 저장과 완전히 분리된 저장 루트를 선택한다.
    /// 프로필 선택값만 PlayerPrefs에 두고, 게임 데이터는 별도 디렉터리에 저장한다.
    /// </summary>
    public static class DevelopmentSaveProfile
    {
        public const string DirectoryName = "development-profile";
        public const string MetaFileName = "meta-stash.json";
        public const string RunFileName = "run-save.json";

        private const string EnabledKey = "project-c.development-save-profile";

        public static bool IsAvailable => Application.isEditor || Debug.isDebugBuild;

        public static bool IsEnabled =>
            IsAvailable && PlayerPrefs.GetInt(EnabledKey, 0) == 1;

        public static string ActiveRootPath =>
            ResolveRoot(Application.persistentDataPath, IsEnabled);

        public static string DevelopmentRootPath =>
            ResolveRoot(Application.persistentDataPath, useDevelopmentProfile: true);

        public static string ActiveLabel => IsEnabled ? "임시 개발 프로필" : "실제 플레이 프로필";

        public static bool HasDevelopmentData =>
            File.Exists(Path.Combine(DevelopmentRootPath, MetaFileName)) ||
            File.Exists(Path.Combine(DevelopmentRootPath, RunFileName)) ||
            Directory.Exists(RunTelemetryStore.ReportDirectoryPath);

        public static void SetEnabled(bool enabled)
        {
            if (!IsAvailable) return;

            PlayerPrefs.SetInt(EnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();

            // 서로 다른 프로필의 체크포인트/영웅 선택이 한 씬에서 섞이지 않게 한다.
            RunSaveStore.ContinueRequested = false;
            HeroSelection.SelectedId = HeroRoster.All[0].Id;
        }

        /// <summary>임시 프로필의 알려진 저장 파일만 삭제한다. 실제 저장 루트는 건드리지 않는다.</summary>
        public static void ClearDevelopmentData()
        {
            if (!IsAvailable) return;

            string root = DevelopmentRootPath;
            DeleteIfPresent(Path.Combine(root, MetaFileName));
            DeleteIfPresent(Path.Combine(root, RunFileName));
            if (Directory.Exists(RunTelemetryStore.ReportDirectoryPath))
                Directory.Delete(RunTelemetryStore.ReportDirectoryPath, recursive: true);

            if (Directory.Exists(root) && Directory.GetFileSystemEntries(root).Length == 0)
                Directory.Delete(root);

            RunSaveStore.ContinueRequested = false;
        }

        public static string ResolveRoot(string persistentRoot, bool useDevelopmentProfile)
        {
            if (string.IsNullOrWhiteSpace(persistentRoot))
                throw new ArgumentException("저장 루트가 비어 있습니다.", nameof(persistentRoot));

            return useDevelopmentProfile
                ? Path.Combine(persistentRoot, DirectoryName)
                : persistentRoot;
        }

        public static string ResolveFile(
            string persistentRoot,
            bool useDevelopmentProfile,
            string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
                throw new ArgumentException("저장 파일명만 허용합니다.", nameof(fileName));

            return Path.Combine(ResolveRoot(persistentRoot, useDevelopmentProfile), fileName);
        }

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
