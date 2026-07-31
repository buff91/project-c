using System;
using System.IO;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 개발용 플레이테스트 리포트 저장소. 실제 플레이 프로필과 무관하게
    /// development-profile/telemetry 아래에 사람이 읽을 수 있는 JSON을 남긴다.
    /// </summary>
    public static class RunTelemetryStore
    {
        public const string DirectoryName = "telemetry";

        public static bool IsAvailable => Application.isEditor || Debug.isDebugBuild;
        public static string ReportDirectoryPath => Path.Combine(
            DevelopmentSaveProfile.DevelopmentRootPath,
            DirectoryName);

        public static string Save(RunTelemetry telemetry)
        {
            if (!IsAvailable || telemetry == null) return null;

            // 구 체크포인트에서 이어진 리포트도 저장 순간부터 당시 표기를 동결한다.
            telemetry.FreezeFloorLabels();
            // 구간 롤업은 파생 값이라 저장 직전에 다시 계산한다 — 리포트만 봐도 구간 비교가 된다.
            telemetry.RefreshBands();

            string directory = ReportDirectoryPath;
            Directory.CreateDirectory(directory);

            string runId = SanitizeFileName(
                string.IsNullOrWhiteSpace(telemetry.runId)
                    ? $"run-{DateTime.UtcNow:yyyyMMddTHHmmssZ}"
                    : telemetry.runId);
            string path = Path.Combine(directory, $"run-{runId}.json");
            File.WriteAllText(path, JsonUtility.ToJson(telemetry, prettyPrint: true));
            return path;
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '-');
            return value;
        }
    }
}
