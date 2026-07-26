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

        public static void Save(RunSaveData data)
        {
            AtomicJsonStore.Save(SavePath, data);
        }

        public static bool TryLoad(out RunSaveData data)
        {
            data = null;
            if (AtomicJsonStore.TryLoad(
                    SavePath,
                    out data,
                    out bool recoveredFromBackup))
            {
                if (recoveredFromBackup)
                    Debug.LogWarning("[Save] 손상된 체크포인트를 백업에서 복구했다.");
                return true;
            }

            if (HasSave)
                Debug.LogWarning("[Save] 체크포인트와 백업을 읽지 못해 이어하기를 무시한다.");
            return false;
        }

        public static void Clear()
        {
            AtomicJsonStore.Clear(SavePath);
        }
    }
}
