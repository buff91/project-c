using System.IO;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>층 체크포인트 저장 파일 입출력. JSON 한 파일, 판 종료 시 삭제.</summary>
    public static class RunSaveStore
    {
        /// <summary>메인 메뉴의 "이어하기"가 켠다. 게임 씬이 소비 후 끈다.</summary>
        public static bool ContinueRequested;

        private static string SavePath => Path.Combine(Application.persistentDataPath, "run-save.json");

        public static bool HasSave => File.Exists(SavePath);

        public static void Save(RunSaveData data)
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        }

        public static bool TryLoad(out RunSaveData data)
        {
            data = null;
            if (!HasSave) return false;
            try
            {
                data = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(SavePath));
                return data != null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Save] 저장 파일 파싱 실패 — 무시하고 삭제: {e.Message}");
                Clear();
                return false;
            }
        }

        public static void Clear()
        {
            if (HasSave) File.Delete(SavePath);
        }
    }
}
