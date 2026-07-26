using System;
using System.IO;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// JSON을 같은 디렉터리의 임시 파일에 먼저 기록한 뒤 교체한다.
    /// 기존 파일은 .bak으로 남겨 중단된 쓰기나 손상된 JSON에서 복구한다.
    /// </summary>
    public static class AtomicJsonStore
    {
        public static string BackupPathFor(string path) => path + ".bak";
        public static string TemporaryPathFor(string path) => path + ".tmp";

        public static bool HasSave(string path) =>
            File.Exists(path) || File.Exists(BackupPathFor(path));

        public static void Save<T>(string path, T data)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("저장 경로가 비어 있다.", nameof(path));
            if (data == null) throw new ArgumentNullException(nameof(data));

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temporaryPath = TemporaryPathFor(path);
            WriteAndFlush(temporaryPath, JsonUtility.ToJson(data));

            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, BackupPathFor(path));
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }

        public static bool TryLoad<T>(string path, out T data, out bool recoveredFromBackup)
            where T : class
        {
            data = null;
            recoveredFromBackup = false;

            if (TryRead(path, out data)) return true;

            string backupPath = BackupPathFor(path);
            if (!TryRead(backupPath, out data)) return false;

            RestorePrimary(path, backupPath);
            recoveredFromBackup = true;
            return true;
        }

        public static void Clear(string path)
        {
            DeleteIfExists(path);
            DeleteIfExists(BackupPathFor(path));
            DeleteIfExists(TemporaryPathFor(path));
        }

        private static bool TryRead<T>(string path, out T data) where T : class
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
            string temporaryPath = TemporaryPathFor(path);
            WriteAndFlush(temporaryPath, File.ReadAllText(backupPath));

            if (File.Exists(path))
                File.Replace(temporaryPath, path, null);
            else
                File.Move(temporaryPath, path);
        }

        private static void WriteAndFlush(string path, string contents)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(contents);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
