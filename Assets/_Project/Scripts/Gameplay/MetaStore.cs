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

        public static MetaSaveData LoadOrNew()
        {
            if (AtomicJsonStore.TryLoad(
                    SavePath,
                    out MetaSaveData data,
                    out bool recoveredFromBackup))
            {
                if (recoveredFromBackup)
                    Debug.LogWarning("[Meta] 손상된 메타 저장을 백업에서 복구했다.");
                // 구세이브(schemaVersion 0)를 최신 스키마로 올린다. 여기와 RunSaveStore 의
                // 로드 지점 둘이 마이그레이션의 유일한 입구다 — 새 로드 경로를 만들면
                // 이 변환을 조용히 우회한다.
                if (SaveMigration.Migrate(data, ItemCatalog.ChargesPerItem))
                    Debug.Log("[Meta] 세이브 스키마를 v" + SaveMigration.CurrentVersion + "로 변환했다.");
                return data;
            }

            if (AtomicJsonStore.HasSave(SavePath))
                Debug.LogWarning("[Meta] 메타 저장과 백업을 읽지 못해 새로 시작한다.");
            // 새 세이브는 이미 최신 스키마다 — 마이그레이션 대상이 아니라고 표시한다.
            var fresh = new MetaSaveData();
            SaveMigration.Stamp(fresh);
            return fresh;
        }

        public static void Save(MetaSaveData data)
        {
            SaveMigration.Stamp(data);
            AtomicJsonStore.Save(SavePath, data);
        }
    }
}
