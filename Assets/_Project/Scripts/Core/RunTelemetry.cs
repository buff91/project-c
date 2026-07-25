using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectC.Core
{
    public enum RunTelemetryOutcome
    {
        InProgress = 0,
        Victory = 1,
        Extraction = 2,
        Defeat = 3,
        Abandoned = 4
    }

    [Serializable]
    public sealed class RunFloorTelemetry
    {
        public int floorIndex;

        /// <summary>
        /// 몇 번째로 방문한 층인가(0부터, 던전 체인 누적). 구간 롤업의 키다 —
        /// floorIndex 부호로 역산하면 상승 던전에서 전부 첫 구간으로 붕괴한다.
        /// </summary>
        public int progressIndex;

        public int visits;
        public int turns;
        public float elapsedSeconds;
        public int damageTaken;
        public int damageDealt;
        public int kills;
        public int itemsCollected;
        public int itemsUsed;
        public int itemsCrafted;
        public int restSitesUsed;
        public int healingFromRest;
        public int secretRoomsFound;
    }

    /// <summary>
    /// 깊이 구간(<see cref="DungeonDepthBand"/>)별 롤업. 층별 계측에서 파생되는 값이라
    /// 따로 기록하지 않고 <see cref="RunTelemetry.RefreshBands"/>가 다시 계산한다 —
    /// 구간 경계가 바뀌어도 과거 리포트가 같은 규칙으로 다시 묶인다.
    /// </summary>
    [Serializable]
    public sealed class RunBandTelemetry
    {
        /// <summary>코드/JSON 키(Shallow/Mid/Deep/Boss). 과거 리포트 호환을 위해 유지한다.</summary>
        public string band;

        /// <summary>화면·리포트용 방향 중립 이름(초반/중반/후반/보스).</summary>
        public string label;

        public string floorRange;
        public int floors;
        public int visits;
        public int turns;
        public float elapsedSeconds;
        public int damageTaken;
        public int damageDealt;
        public int kills;
        public int itemsCollected;
        public int itemsUsed;
        public int itemsCrafted;
        public int restSitesUsed;
        public int healingFromRest;
        public int secretRoomsFound;
    }

    [Serializable]
    public sealed class RunDamageTelemetry
    {
        public string source;
        public int incomingHits;
        public int damageTaken;
        public int outgoingHits;
        public int damageDealt;
        public int fatalHits;
    }

    [Serializable]
    public sealed class RunItemTelemetry
    {
        public string itemId;
        public int collected;
        public int used;
        public int crafted;
    }

    /// <summary>
    /// 한 판의 플레이테스트 계측. 순수 데이터/집계만 소유하며 Unity 시간·파일 API는 모른다.
    /// Gameplay가 실제 이벤트와 unscaled delta time을 전달한다.
    /// </summary>
    [Serializable]
    public sealed class RunTelemetry
    {
        public const int CurrentSchemaVersion = 5;

        /// <summary>구간 롤업을 항상 얕은 곳부터 깊은 곳 순으로 낸다.</summary>
        private static readonly DungeonDepthBand[] BandOrder =
        {
            DungeonDepthBand.Shallow,
            DungeonDepthBand.Mid,
            DungeonDepthBand.Deep,
            DungeonDepthBand.Boss
        };

        public int schemaVersion = CurrentSchemaVersion;
        public string runId;
        public string dungeonId;
        public string heroId;
        public int seed;
        public string startedAtUtc;
        public string endedAtUtc;
        public RunTelemetryOutcome outcome;
        public string outcomeLabel;
        public string endCause;
        public int currentFloorIndex;
        public int deepestFloorIndex;

        /// <summary>현재/최대 진행 지수. 구간 롤업과 "얼마나 나아갔나"의 출처다.</summary>
        public int currentProgressIndex;
        public int deepestProgressIndex;
        public int totalTurns;
        public float elapsedSeconds;
        public int totalDamageTaken;
        public int totalDamageDealt;
        public int kills;
        public int bossKills;
        public int itemsCollected;
        public int itemsUsed;
        public int itemsCrafted;
        public int waitActions;
        public int meleeAttacks;
        public int rangedAttacks;
        public int doorInteractions;
        public int secretRoomsFound;
        public int barrelPushes;
        public int playerFalls;
        public int enemyFalls;
        public int intentionalFalls;
        public int floorsFallen;
        public int burnApplications;
        public int freezeApplications;
        public int oilIgnitedTiles;
        public int waterFrozenTiles;
        public int waterEvaporatedTiles;
        public int restSitesUsed;
        public int healingFromRest;
        public int starvingTurns;
        public int starvationDamage;
        public bool cheatsUsed;
        public List<RunFloorTelemetry> floors = new List<RunFloorTelemetry>();
        public List<RunDamageTelemetry> damageSources = new List<RunDamageTelemetry>();
        public List<RunItemTelemetry> items = new List<RunItemTelemetry>();

        /// <summary>층별 계측에서 파생되는 깊이 구간 롤업. <see cref="RefreshBands"/>가 채운다.</summary>
        public List<RunBandTelemetry> bands = new List<RunBandTelemetry>();

        public bool Ended => outcome != RunTelemetryOutcome.InProgress;

        public static RunTelemetry Begin(
            string dungeonId,
            string heroId,
            int seed,
            int floorIndex,
            DateTime utcNow,
            int progressIndex = 0)
        {
            var telemetry = new RunTelemetry
            {
                runId = $"{utcNow:yyyyMMddTHHmmssfffZ}-{seed}",
                dungeonId = dungeonId ?? "",
                heroId = heroId ?? "",
                seed = seed,
                startedAtUtc = utcNow.ToString("O"),
                outcome = RunTelemetryOutcome.InProgress,
                outcomeLabel = RunTelemetryOutcome.InProgress.ToString(),
                currentFloorIndex = floorIndex,
                deepestFloorIndex = floorIndex,
                currentProgressIndex = progressIndex,
                deepestProgressIndex = progressIndex
            };
            telemetry.RecordFloorEntered(floorIndex, progressIndex);
            return telemetry;
        }

        public void RecordElapsed(float seconds, int floorIndex)
        {
            if (Ended || seconds <= 0f) return;
            EnsureCurrentFloor(floorIndex);
            elapsedSeconds += seconds;
            Floor(floorIndex).elapsedSeconds += seconds;
        }

        public void RecordTurn(int floorIndex)
        {
            if (Ended) return;
            EnsureCurrentFloor(floorIndex);
            totalTurns++;
            Floor(floorIndex).turns++;
        }

        public void RecordFloorEntered(int floorIndex, int progressIndex)
        {
            if (Ended) return;
            currentFloorIndex = floorIndex;
            currentProgressIndex = progressIndex;
            // 가장 멀리 간 층은 진행 지수로 고른다 — 층 인덱스 최솟값을 쓰면 상승 던전에서
            // 시작 층이 영원히 "최심층"으로 남고, 비단조 경로에서도 답이 아니다.
            if (progressIndex >= deepestProgressIndex)
            {
                deepestProgressIndex = progressIndex;
                deepestFloorIndex = floorIndex;
            }

            RunFloorTelemetry floor = Floor(floorIndex);
            floor.progressIndex = progressIndex;
            floor.visits++;
        }

        public void RecordDamageTaken(string source, int amount, bool fatal, int floorIndex)
        {
            if (Ended || amount <= 0) return;
            EnsureCurrentFloor(floorIndex);
            totalDamageTaken += amount;
            Floor(floorIndex).damageTaken += amount;
            RunDamageTelemetry damage = Damage(source);
            damage.incomingHits++;
            damage.damageTaken += amount;
            if (fatal) damage.fatalHits++;
        }

        public void RecordDamageDealt(string source, int amount, int floorIndex)
        {
            if (Ended || amount <= 0) return;
            EnsureCurrentFloor(floorIndex);
            totalDamageDealt += amount;
            Floor(floorIndex).damageDealt += amount;
            RunDamageTelemetry damage = Damage(source);
            damage.outgoingHits++;
            damage.damageDealt += amount;
        }

        public void RecordKill(int floorIndex, bool boss)
        {
            if (Ended) return;
            EnsureCurrentFloor(floorIndex);
            kills++;
            if (boss) bossKills++;
            Floor(floorIndex).kills++;
        }

        public void RecordItemCollected(ItemKind kind, int floorIndex)
        {
            if (Ended) return;
            EnsureCurrentFloor(floorIndex);
            itemsCollected++;
            Floor(floorIndex).itemsCollected++;
            Item(kind).collected++;
        }

        public void RecordItemUsed(ItemKind kind, int floorIndex)
        {
            if (Ended) return;
            EnsureCurrentFloor(floorIndex);
            itemsUsed++;
            Floor(floorIndex).itemsUsed++;
            Item(kind).used++;
        }

        public void RecordItemCrafted(ItemKind kind, int floorIndex)
        {
            if (Ended) return;
            EnsureCurrentFloor(floorIndex);
            itemsCrafted++;
            Floor(floorIndex).itemsCrafted++;
            Item(kind).crafted++;
        }

        public void RecordFall(bool player, bool intentional, int fallenFloorCount)
        {
            if (Ended) return;
            if (player) playerFalls++;
            else enemyFalls++;
            if (intentional) intentionalFalls++;
            floorsFallen += Math.Max(0, fallenFloorCount);
        }

        public void RecordStatus(StatusKind kind)
        {
            if (Ended) return;
            if (kind == StatusKind.Burn) burnApplications++;
            else if (kind == StatusKind.Freeze) freezeApplications++;
        }

        public void RecordOilIgnition(int tileCount)
        {
            if (!Ended) oilIgnitedTiles += Math.Max(0, tileCount);
        }

        public void RecordWaterFreeze(int tileCount)
        {
            if (!Ended) waterFrozenTiles += Math.Max(0, tileCount);
        }

        public void RecordWaterEvaporation(int tileCount)
        {
            if (!Ended) waterEvaporatedTiles += Math.Max(0, tileCount);
        }

        public void RecordRest(int healed, int floorIndex)
        {
            if (Ended || healed <= 0) return;
            EnsureCurrentFloor(floorIndex);
            restSitesUsed++;
            healingFromRest += healed;
            RunFloorTelemetry floor = Floor(floorIndex);
            floor.restSitesUsed++;
            floor.healingFromRest += healed;
        }

        /// <summary>굶주림으로 깎인 턴·피해. 배고픔 압박이 실제로 물렸는지 리포트로 본다.</summary>
        public void RecordStarvation(int damage)
        {
            if (Ended) return;
            starvingTurns++;
            starvationDamage += Math.Max(0, damage);
        }

        public void RecordSecretRoomFound(int floorIndex)
        {
            if (Ended) return;
            EnsureCurrentFloor(floorIndex);
            secretRoomsFound++;
            Floor(floorIndex).secretRoomsFound++;
        }

        public void End(RunTelemetryOutcome result, string cause, DateTime utcNow)
        {
            if (Ended || result == RunTelemetryOutcome.InProgress) return;
            outcome = result;
            outcomeLabel = result.ToString();
            endCause = cause ?? "";
            endedAtUtc = utcNow.ToString("O");
            RefreshBands();
        }

        /// <summary>
        /// 층별 계측을 깊이 구간으로 다시 묶는다. 저장·요약 직전에 부르며, 파생 값이라
        /// 몇 번을 불러도 결과가 같다. 방문하지 않은 구간은 리포트에 넣지 않는다.
        /// </summary>
        public void RefreshBands()
        {
            bands.Clear();
            foreach (DungeonDepthBand band in BandOrder)
            {
                RunBandTelemetry rolled = null;
                foreach (RunFloorTelemetry floor in floors)
                {
                    // 진행 지수로 묶는다. 예전에는 floorIndex 부호로 역산했는데(ForFloor),
                    // 상승 던전에서는 전부 첫 구간으로 붕괴했다.
                    if (DungeonDepthBandRules.ForDepth(floor.progressIndex) != band) continue;
                    if (rolled == null)
                    {
                        rolled = new RunBandTelemetry
                        {
                            band = band.ToString(),
                            label = DungeonDepthBandRules.BandLabel(band),
                            floorRange = DungeonDepthBandRules.RangeLabel(band)
                        };
                    }

                    rolled.floors++;
                    rolled.visits += floor.visits;
                    rolled.turns += floor.turns;
                    rolled.elapsedSeconds += floor.elapsedSeconds;
                    rolled.damageTaken += floor.damageTaken;
                    rolled.damageDealt += floor.damageDealt;
                    rolled.kills += floor.kills;
                    rolled.itemsCollected += floor.itemsCollected;
                    rolled.itemsUsed += floor.itemsUsed;
                    rolled.itemsCrafted += floor.itemsCrafted;
                    rolled.restSitesUsed += floor.restSitesUsed;
                    rolled.healingFromRest += floor.healingFromRest;
                    rolled.secretRoomsFound += floor.secretRoomsFound;
                }

                if (rolled != null) bands.Add(rolled);
            }
        }

        public string FormatCompactSummary()
        {
            string source = TopIncomingDamageSource();
            string sourceText = string.IsNullOrEmpty(source) ? "--" : source;
            return
                $"RUN {FormatDuration(elapsedSeconds)} · 턴 {totalTurns} · " +
                $"{FormatFloor(currentFloorIndex)} (최고 도달 {FormatFloor(deepestFloorIndex)})\n" +
                $"피해 {totalDamageTaken} ({sourceText}) · 가한 피해 {totalDamageDealt} · 처치 {kills}\n" +
                $"획득 {itemsCollected} · 사용 {itemsUsed} · 조합 {itemsCrafted} · " +
                $"낙하 P{playerFalls}/E{enemyFalls}\n" +
                $"숨은 방 {secretRoomsFound} · 휴식 {restSitesUsed}회/+{healingFromRest} HP · " +
                $"굶주림 {starvingTurns}턴/-{starvationDamage} HP · " +
                $"상태 화상 {burnApplications}/빙결 {freezeApplications} · " +
                $"반응 기름 {oilIgnitedTiles}/물결빙 {waterFrozenTiles}/증발 {waterEvaporatedTiles}" +
                (cheatsUsed ? "\n⚠ CHEATS USED" : "");
        }

        public string FormatDetailedSummary()
        {
            var text = new StringBuilder(FormatCompactSummary());
            text.Append("\n구간별:\n");
            text.Append(FormatBandSummary());
            text.Append("\n층별:");
            foreach (RunFloorTelemetry floor in floors)
            {
                text.Append($"\n- {FormatFloor(floor.floorIndex)} " +
                            $"{FormatDuration(floor.elapsedSeconds)} / {floor.turns}턴 / " +
                            $"피해 {floor.damageTaken} / 처치 {floor.kills} / 획득 {floor.itemsCollected}");
            }
            return text.ToString();
        }

        /// <summary>
        /// 깊이 구간 비교용 한 줄씩 요약. 같은 리포트 안에서 "어느 구간이 오래 걸리고 아팠는가"를
        /// 바로 읽을 수 있게 체류(시간/턴)·피해·처치·아이템·휴식·숨은 방을 나란히 둔다.
        /// </summary>
        public string FormatBandSummary()
        {
            RefreshBands();
            if (bands.Count == 0) return "구간 데이터 없음";

            var text = new StringBuilder();
            for (int i = 0; i < bands.Count; i++)
            {
                RunBandTelemetry band = bands[i];
                if (i > 0) text.Append('\n');
                text.Append(
                    $"- {band.label} {band.floorRange} · {FormatDuration(band.elapsedSeconds)}/{band.turns}턴 " +
                    $"({band.floors}층) · 피해 {band.damageTaken} / 가한 피해 {band.damageDealt} · " +
                    $"처치 {band.kills} · 아이템 {band.itemsCollected}획득·{band.itemsUsed}사용 · " +
                    $"휴식 {band.restSitesUsed}회/+{band.healingFromRest} HP · 숨은 방 {band.secretRoomsFound}");
            }
            return text.ToString();
        }

        public static string FormatFloor(int floorIndex) =>
            $"B{Math.Abs(floorIndex) + 1}";

        public static string FormatDuration(float seconds)
        {
            int total = Math.Max(0, (int)seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }

        private void EnsureCurrentFloor(int floorIndex)
        {
            if (currentFloorIndex == floorIndex) return;

            // 이 경로는 진행 지수를 모른다(피해·아이템 기록 등). 이미 방문한 층이면 그때 기록한
            // 값을 재사용하고, 처음 보는 층이면 현재 진행 지수를 유지한다 —
            // 정상 흐름은 언제나 RecordFloorEntered 가 먼저 돈다.
            int progress = currentProgressIndex;
            foreach (RunFloorTelemetry floor in floors)
            {
                if (floor.floorIndex != floorIndex) continue;
                progress = floor.progressIndex;
                break;
            }

            RecordFloorEntered(floorIndex, progress);
        }

        private RunFloorTelemetry Floor(int floorIndex)
        {
            foreach (RunFloorTelemetry floor in floors)
                if (floor.floorIndex == floorIndex)
                    return floor;

            var created = new RunFloorTelemetry
            {
                floorIndex = floorIndex,
                progressIndex = currentProgressIndex
            };
            floors.Add(created);

            // 방문 순서로 정렬한다. 예전에는 floorIndex 내림차순(= 깊을수록 먼저)이었는데
            // 상승 던전에서는 순서가 뒤집힌다. 진행 지수는 방향과 무관하게 항상 방문 순서다.
            floors.Sort((a, b) => a.progressIndex.CompareTo(b.progressIndex));
            return created;
        }

        private RunDamageTelemetry Damage(string source)
        {
            string normalized = NormalizeDamageSource(source);
            foreach (RunDamageTelemetry damage in damageSources)
                if (damage.source == normalized)
                    return damage;

            var created = new RunDamageTelemetry { source = normalized };
            damageSources.Add(created);
            return created;
        }

        private RunItemTelemetry Item(ItemKind kind)
        {
            string id = kind.ToString();
            foreach (RunItemTelemetry item in items)
                if (item.itemId == id)
                    return item;

            var created = new RunItemTelemetry { itemId = id };
            items.Add(created);
            return created;
        }

        private string TopIncomingDamageSource()
        {
            RunDamageTelemetry top = null;
            foreach (RunDamageTelemetry damage in damageSources)
            {
                if (damage.damageTaken <= 0) continue;
                if (top == null || damage.damageTaken > top.damageTaken)
                    top = damage;
            }
            return top == null ? null : $"{top.source} {top.damageTaken}";
        }

        private static string NormalizeDamageSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return "Unknown";
            MonsterArchetype monster = MonsterRoster.MatchSource(source);
            if (monster != null) return monster.Id;
            return source;
        }
    }
}
