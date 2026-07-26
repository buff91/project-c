using System;
using System.IO;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// JSON 저장을 임시 파일에 먼저 완성한 뒤 교체하고, 직전 정상본을 백업으로 남긴다.
    /// Unity 직렬화와 파일 시스템은 Gameplay 계층 책임이므로 Core로 내리지 않는다.
    /// </summary>
    public static class JsonFileStore
    {
        public const string BackupSuffix = ".bak";
        public const string TemporarySuffix = ".tmp";

        public static bool Exists(string path) =>
            File.Exists(path) || File.Exists(BackupPath(path));

        public static void Save<T>(string path, T data)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("저장 경로가 비어 있다.", nameof(path));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = TemporaryPath(path);
            try
            {
                WriteFully(temporaryPath, JsonUtility.ToJson(data));
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, BackupPath(path));
                else
                    File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        public static bool TryLoad<T>(string path, out T data, out bool recoveredFromBackup)
            where T : class
        {
            recoveredFromBackup = false;
            if (TryRead(path, out data))
                return true;

            string backupPath = BackupPath(path);
            if (!TryRead(backupPath, out data))
                return false;

            recoveredFromBackup = true;
            RestorePrimary(path, backupPath);
            return true;
        }

        public static void Clear(string path)
        {
            DeleteIfExists(path);
            DeleteIfExists(BackupPath(path));
            DeleteIfExists(TemporaryPath(path));
        }

        public static string BackupPath(string path) => path + BackupSuffix;
        public static string TemporaryPath(string path) => path + TemporarySuffix;

        private static bool TryRead<T>(string path, out T data)
            where T : class
        {
            data = null;
            if (!File.Exists(path)) return false;

            try
            {
                data = JsonUtility.FromJson<T>(File.ReadAllText(path));
                return data != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void RestorePrimary(string path, string backupPath)
        {
            string temporaryPath = TemporaryPath(path);
            try
            {
                File.Copy(backupPath, temporaryPath, true);
                if (File.Exists(path))
                {
                    string corruptPath = path + ".corrupt";
                    DeleteIfExists(corruptPath);
                    File.Replace(temporaryPath, path, corruptPath);
                    DeleteIfExists(corruptPath);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            finally
            {
                DeleteIfExists(temporaryPath);
            }
        }

        private static void WriteFully(string path, string contents)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(contents);
                writer.Flush();
                stream.Flush(true);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
