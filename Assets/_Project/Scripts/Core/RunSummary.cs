using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 한 판의 결과 기록. 사망/승리 시 게임오버 화면이 읽는다.
    /// <para>
    /// "가장 멀리 간 층"은 <b>진행 지수</b>로 판정한다 — 고도가 아니다.
    /// 예전에는 층 인덱스의 최솟값을 썼는데(아래로 갈수록 작다는 전제), 상승 던전에서는
    /// 시작 층이 영원히 최솟값이라 도달 층이 첫 층에 붙어 있었다.
    /// 비단조 경로(올라갔다 떨어지는 층)에서도 최솟값은 답이 아니다.
    /// </para>
    /// </summary>
    public sealed class RunSummary
    {
        /// <summary>가장 멀리 진행한 층의 <b>층 인덱스</b>(표시·세이브용). 진행 지수로 골라낸 값이다.</summary>
        public int DeepestFloorIndex { get; private set; }

        /// <summary>가장 멀리 진행한 <b>진행 지수</b>. 어느 층이 더 멀리 갔는지의 판정 기준.</summary>
        public int FurthestProgressIndex { get; private set; }

        public string CauseOfDeath { get; private set; }
        public int Kills { get; private set; }
        public bool Victory { get; private set; }
        public bool Extracted { get; private set; }
        public int GoldBanked { get; private set; }
        public bool Ended { get; private set; }

        public RunSummary(int startFloorIndex = 0, int kills = 0, int startProgressIndex = 0)
        {
            DeepestFloorIndex = startFloorIndex;
            FurthestProgressIndex = startProgressIndex;
            Kills = kills;
        }

        /// <summary>
        /// 층 방문 기록. <paramref name="progressIndex"/>가 지금까지의 최대보다 크지 않으면 무시한다 —
        /// 되돌아간 층이 "도달 층"을 되돌리지 않는다.
        /// </summary>
        public void RecordFloor(int floorIndex, int progressIndex)
        {
            if (progressIndex < FurthestProgressIndex) return;
            FurthestProgressIndex = progressIndex;
            DeepestFloorIndex = floorIndex;
        }

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
            MonsterArchetype monster = MonsterRoster.MatchSource(source);
            if (monster != null) return monster.DisplayName;

            // 여기 없는 소스는 영문 토큰이 그대로 결과 화면에 뜬다("사인: Starving").
            // 몬스터가 아닌 사인은 전부 `ShowPlayerHit(damage, source)` 호출부에서 오므로,
            // 새 사인을 만들 때는 그 자리에서 이 표도 함께 늘린다.
            switch (source)
            {
                case "Burn": return "화상";
                case "Fall": return "낙하";
                case "Crush": return "낙하 충돌";
                case "Bomb": return "폭발";
                case "Starving": return "굶주림";
                case "Poison": return "중독";
                case "ArcShock": return "감전";
                default: return source;
            }
        }
    }
}
