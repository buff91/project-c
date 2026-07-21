using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 한 판의 결과 기록. 사망/승리 시 게임오버 화면이 읽는다.
    /// 층 인덱스는 아래로 갈수록 작다(B1=0, B2=-1 …) — 최심층 = 최솟값.
    /// </summary>
    public sealed class RunSummary
    {
        public int DeepestFloorIndex { get; private set; }
        public string CauseOfDeath { get; private set; }
        public int Kills { get; private set; }
        public bool Victory { get; private set; }
        public bool Extracted { get; private set; }
        public int GoldBanked { get; private set; }
        public bool Ended { get; private set; }

        public RunSummary(int startFloorIndex = 0, int kills = 0)
        {
            DeepestFloorIndex = startFloorIndex;
            Kills = kills;
        }

        public void RecordFloor(int floorIndex) =>
            DeepestFloorIndex = Math.Min(DeepestFloorIndex, floorIndex);

        public void RecordKill() => Kills++;

        /// <summary>사망으로 판 종료. 최초 사인만 유지한다(연쇄 피해 방어).</summary>
        public void EndInDefeat(string cause)
        {
            if (Ended) return;
            Ended = true;
            Victory = false;
            CauseOfDeath = string.IsNullOrWhiteSpace(cause) ? "UNKNOWN" : cause;
        }

        public void EndInVictory(int goldBanked = 0)
        {
            if (Ended) return;
            Ended = true;
            Victory = true;
            GoldBanked = goldBanked;
        }

        /// <summary>생환(extraction): 승리는 아니지만 전리품을 챙겨 살아 나갔다.</summary>
        public void EndInExtraction(int goldBanked)
        {
            if (Ended) return;
            Ended = true;
            Extracted = true;
            GoldBanked = goldBanked;
        }

        /// <summary>영문 사인 소스("Goblin B2-1", "Burn" …)를 표시용 한글 문구로 바꾼다.</summary>
        public static string FormatCause(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return "알 수 없음";
            if (source.StartsWith("Goblin", StringComparison.Ordinal)) return "고블린";
            if (source.StartsWith("Skeleton", StringComparison.Ordinal)) return "해골";
            if (source.StartsWith("Slime", StringComparison.Ordinal)) return "슬라임";

            switch (source)
            {
                case "Burn": return "화상";
                case "Fall": return "낙하";
                case "Crush": return "낙하 충돌";
                case "Bomb": return "폭발";
                default: return source;
            }
        }
    }
}
