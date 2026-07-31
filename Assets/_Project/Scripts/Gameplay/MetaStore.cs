using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>메타 창고 파일 입출력. 원자적 JSON + 직전 정상 백업, 판 종료에도 유지된다.</summary>
    public static class MetaStore
    {
        private static string SavePath => System.IO.Path.Combine(
            DevelopmentSaveProfile.ActiveRootPath,
            DevelopmentSaveProfile.MetaFileName);

        /// <summary>
        /// 현재 메타 파일과 백업을 이 빌드가 손실 없이 다시 쓸 수 있는가.
        /// 미래 스키마가 하나라도 있으면 허브 변경과 원정 시작을 막는 상위 흐름의 게이트다.
        /// </summary>
        public static bool CanWrite => IsWriteCompatible(SavePath);

        public static MetaSaveData LoadOrNew() => LoadOrNew(SavePath);

        internal static MetaSaveData LoadOrNew(string path)
        {
            bool hasFutureSchema = ExistingSaveHasFutureSchema(path);
            if (AtomicJsonStore.TryLoad(
                    path,
                    out MetaSaveData data,
                    out bool recoveredFromBackup))
            {
                if (recoveredFromBackup)
                    Debug.LogWarning("[Meta] 손상된 메타 저장을 백업에서 복구했다.");
                if (hasFutureSchema)
                {
                    Debug.LogWarning(
                        "[Meta] 현재 빌드보다 새로운 메타 저장은 알려진 값만 읽고, " +
                        "알 수 없는 필드를 보존하기 위해 다시 쓰지 않는다.");
                    return data;
                }
                // 구세이브(schemaVersion 0)를 최신 스키마로 올린다. 여기와 RunSaveStore 의
                // 로드 지점 둘이 마이그레이션의 유일한 입구다 — 새 로드 경로를 만들면
                // 이 변환을 조용히 우회한다.
                if (SaveMigration.Migrate(data, ItemCatalog.ChargesPerItem))
                    Debug.Log("[Meta] 세이브 스키마를 v" + SaveMigration.CurrentVersion + "로 변환했다.");
                return data;
            }

            if (AtomicJsonStore.HasSave(path))
                Debug.LogWarning("[Meta] 메타 저장과 백업을 읽지 못해 새로 시작한다.");
            // 새 세이브는 이미 최신 스키마다 — 마이그레이션 대상이 아니라고 표시한다.
            var fresh = new MetaSaveData();
            SaveMigration.Stamp(fresh);
            return fresh;
        }

        internal static bool IsWriteCompatible(string path) =>
            !ExistingSaveHasFutureSchema(path);

        /// <returns>저장했으면 true. 미래 버전 파일을 보호해 쓰기를 거부했으면 false.</returns>
        public static bool Save(MetaSaveData data) => Save(SavePath, data);

        internal static bool Save(string path, MetaSaveData data)
        {
            if (SaveMigration.HasFutureSchema(data) || ExistingSaveHasFutureSchema(path))
            {
                Debug.LogWarning(
                    "[Meta] 현재 빌드보다 새로운 메타 저장은 알 수 없는 필드를 보존하기 위해 " +
                    "덮어쓰지 않는다.");
                return false;
            }

            SaveMigration.Stamp(data);
            AtomicJsonStore.Save(path, data);
            return true;
        }

        private static bool ExistingSaveHasFutureSchema(string path)
        {
            if (AtomicJsonStore.TryLoadExact(
                    path,
                    out MetaSaveData primary) &&
                SaveMigration.HasFutureSchema(primary))
                return true;

            string backupPath = AtomicJsonStore.BackupPathFor(path);
            return AtomicJsonStore.TryLoadExact(
                       backupPath,
                       out MetaSaveData backup) &&
                   SaveMigration.HasFutureSchema(backup);
        }
    }
}
