using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>메타 창고 파일 입출력. 판 종료(사망 포함)에도 유지된다.</summary>
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
                return data;
            }

            if (AtomicJsonStore.HasSave(SavePath))
                Debug.LogWarning("[Meta] 메타 저장과 백업을 읽지 못해 새로 시작한다.");
            return new MetaSaveData();
        }

        public static void Save(MetaSaveData data)
        {
            AtomicJsonStore.Save(SavePath, data);
        }
    }
}
