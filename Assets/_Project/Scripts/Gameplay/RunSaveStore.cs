using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>층 체크포인트 저장 파일 입출력. 원자적 JSON + 직전 정상 백업, 판 종료 시 삭제.</summary>
    public static class RunSaveStore
    {
        /// <summary>메인 메뉴의 "이어하기"가 켠다. 게임 씬이 소비 후 끈다.</summary>
        public static bool ContinueRequested;

        private static string SavePath => System.IO.Path.Combine(
            DevelopmentSaveProfile.ActiveRootPath,
            DevelopmentSaveProfile.RunFileName);

        public static bool HasSave => AtomicJsonStore.HasSave(SavePath);

        /// <summary>
        /// 호환되는 미완료 체크포인트만 이어하기로 노출한다. 메타에 같은 runId의 종료 영수증이
        /// 있으면 메타 저장 성공 뒤 삭제 전에 앱이 종료된 잔여 파일이므로 다시 플레이하지 않는다.
        /// </summary>
        public static bool CanResume
        {
            get
            {
                if (!TryLoad(SavePath, out RunSaveData data)) return false;

                string runId = RunSettlementIdentity.Resolve(
                    data.telemetry,
                    data.dungeonId,
                    data.seed);
                return !MetaStore.LoadOrNew().TryGetRunSettlement(runId, out _);
            }
        }

        /// <returns>저장했으면 true. 미래 버전 파일을 보호해 쓰기를 거부했으면 false.</returns>
        public static bool Save(RunSaveData data) => Save(SavePath, data);

        internal static bool Save(string path, RunSaveData data)
        {
            if (SaveMigration.HasFutureSchema(data) || ExistingSaveHasFutureSchema(path))
            {
                Debug.LogWarning(
                    "[Save] 현재 빌드보다 새로운 체크포인트는 알 수 없는 필드를 보존하기 위해 " +
                    "덮어쓰지 않는다. 새 원정을 시작해 체크포인트를 명시적으로 지운 뒤 다시 저장할 수 있다.");
                return false;
            }

            SaveMigration.Stamp(data);
            AtomicJsonStore.Save(path, data);
            return true;
        }

        public static bool TryLoad(out RunSaveData data) => TryLoad(SavePath, out data);

        internal static bool TryLoad(string path, out RunSaveData data)
        {
            data = null;
            if (ExistingSaveHasFutureSchema(path))
            {
                Debug.LogWarning(
                    "[Save] 현재 빌드보다 새로운 체크포인트라 이어하기를 막고 원본을 보존한다.");
                return false;
            }

            if (AtomicJsonStore.TryLoad(
                    path,
                    out data,
                    out bool recoveredFromBackup,
                    out string serializedData))
            {
                if (recoveredFromBackup)
                    Debug.LogWarning("[Save] 손상된 체크포인트를 백업에서 복구했다.");
                if (SaveMigration.Migrate(
                        data,
                        ItemCatalog.ChargesPerItem,
                        SaveMigration.HasSerializedRangedCharges(serializedData)))
                    Debug.Log("[Save] 체크포인트 스키마를 v" + SaveMigration.CurrentVersion + "로 변환했다.");
                return true;
            }

            if (AtomicJsonStore.HasSave(path))
                Debug.LogWarning("[Save] 체크포인트와 백업을 읽지 못해 이어하기를 무시한다.");
            return false;
        }

        public static void Clear()
        {
            AtomicJsonStore.Clear(SavePath);
        }

        private static bool ExistingSaveHasFutureSchema(string path)
        {
            if (AtomicJsonStore.TryLoadExact(
                    path,
                    out RunSaveData primary) &&
                SaveMigration.HasFutureSchema(primary))
                return true;

            string backupPath = AtomicJsonStore.BackupPathFor(path);
            return AtomicJsonStore.TryLoadExact(
                       backupPath,
                       out RunSaveData backup) &&
                   SaveMigration.HasFutureSchema(backup);
        }
    }
}
