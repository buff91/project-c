using System.IO;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>메타 창고 파일 입출력. 원자적 JSON + 직전 정상 백업, 판 종료에도 유지된다.</summary>
    public static class MetaStore
    {
        private static string SavePath => Path.Combine(
            DevelopmentSaveProfile.ActiveRootPath,
            DevelopmentSaveProfile.MetaFileName);

        public static MetaSaveData LoadOrNew()
        {
            if (!JsonFileStore.Exists(SavePath)) return new MetaSaveData();
            if (JsonFileStore.TryLoad(SavePath, out MetaSaveData data, out bool recoveredFromBackup))
            {
                if (recoveredFromBackup)
                    Debug.LogWarning("[Meta] 저장 파일 손상 — 직전 정상 백업을 복구했다.");
                return data;
            }

            Debug.LogWarning("[Meta] 저장 파일과 백업을 읽지 못해 새로 시작한다.");
            return new MetaSaveData();
        }

        public static void Save(MetaSaveData data)
        {
            JsonFileStore.Save(SavePath, data);
        }
    }
}
