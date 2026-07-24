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
        public int visits;
        public int turns;
        public float elapsedSeconds;
        public int damageTaken;
        public int damageDealt;
        public int kills;
        public int itemsCollected;
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
        public const int CurrentSchemaVersion = 1;

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
        public bool cheatsUsed;
        public List<RunFloorTelemetry> floors = new List<RunFloorTelemetry>();
        public List<RunDamageTelemetry> damageSources = new List<RunDamageTelemetry>();
        public List<RunItemTelemetry> items = new List<RunItemTelemetry>();

        public bool Ended => outcome != RunTelemetryOutcome.InProgress;

        public static RunTelemetry Begin(
            string dungeonId,
            string heroId,
            int seed,
            int floorIndex,
            DateTime utcNow)
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
                deepestFloorIndex = floorIndex
            };
            telemetry.RecordFloorEntered(floorIndex);
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

        public void RecordFloorEntered(int floorIndex)
        {
            if (Ended) return;
            currentFloorIndex = floorIndex;
            deepestFloorIndex = Math.Min(deepestFloorIndex, floorIndex);
            Floor(floorIndex).visits++;
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

        public void RecordItemUsed(ItemKind kind)
        {
            if (Ended) return;
            itemsUsed++;
            Item(kind).used++;
        }

        public void RecordItemCrafted(ItemKind kind)
        {
            if (Ended) return;
            itemsCrafted++;
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

        public void End(RunTelemetryOutcome result, string cause, DateTime utcNow)
        {
            if (Ended || result == RunTelemetryOutcome.InProgress) return;
            outcome = result;
            outcomeLabel = result.ToString();
            endCause = cause ?? "";
            endedAtUtc = utcNow.ToString("O");
        }

        public string FormatCompactSummary()
        {
            string source = TopIncomingDamageSource();
            string sourceText = string.IsNullOrEmpty(source) ? "--" : source;
            return
                $"RUN {FormatDuration(elapsedSeconds)} · 턴 {totalTurns} · " +
                $"{FormatFloor(currentFloorIndex)} (최심층 {FormatFloor(deepestFloorIndex)})\n" +
                $"피해 {totalDamageTaken} ({sourceText}) · 가한 피해 {totalDamageDealt} · 처치 {kills}\n" +
                $"획득 {itemsCollected} · 사용 {itemsUsed} · 조합 {itemsCrafted} · " +
                $"낙하 P{playerFalls}/E{enemyFalls}\n" +
                $"상태 화상 {burnApplications}/빙결 {freezeApplications} · " +
                $"반응 기름 {oilIgnitedTiles}/물결빙 {waterFrozenTiles}/증발 {waterEvaporatedTiles}" +
                (cheatsUsed ? "\n⚠ CHEATS USED" : "");
        }

        public string FormatDetailedSummary()
        {
            var text = new StringBuilder(FormatCompactSummary());
            text.Append("\n층별:");
            foreach (RunFloorTelemetry floor in floors)
            {
                text.Append($"\n- {FormatFloor(floor.floorIndex)} " +
                            $"{FormatDuration(floor.elapsedSeconds)} / {floor.turns}턴 / " +
                            $"피해 {floor.damageTaken} / 처치 {floor.kills} / 획득 {floor.itemsCollected}");
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
            if (currentFloorIndex != floorIndex)
                RecordFloorEntered(floorIndex);
        }

        private RunFloorTelemetry Floor(int floorIndex)
        {
            foreach (RunFloorTelemetry floor in floors)
                if (floor.floorIndex == floorIndex)
                    return floor;

            var created = new RunFloorTelemetry { floorIndex = floorIndex };
            floors.Add(created);
            floors.Sort((a, b) => b.floorIndex.CompareTo(a.floorIndex));
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
            if (source.StartsWith("Goblin", StringComparison.Ordinal)) return "Goblin";
            if (source.StartsWith("Skeleton", StringComparison.Ordinal)) return "Skeleton";
            if (source.StartsWith("Slime", StringComparison.Ordinal)) return "Slime";
            return source;
        }
    }
}
