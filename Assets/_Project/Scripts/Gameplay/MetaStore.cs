using System.IO;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>메타 창고 파일 입출력. 판 종료(사망 포함)에도 유지된다.</summary>
    public static class MetaStore
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "meta-stash.json");

        public static MetaSaveData LoadOrNew()
        {
            if (!File.Exists(SavePath)) return new MetaSaveData();
            try
            {
                return JsonUtility.FromJson<MetaSaveData>(File.ReadAllText(SavePath)) ?? new MetaSaveData();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Meta] 창고 파일 파싱 실패 — 새로 시작: {e.Message}");
                return new MetaSaveData();
            }
        }

        public static void Save(MetaSaveData data) =>
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
    }
}
