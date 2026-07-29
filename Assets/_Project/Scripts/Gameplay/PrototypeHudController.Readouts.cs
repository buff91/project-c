using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 640×360 캔버스에서 새로 생긴 세 읽을거리 — 층 스택(D) · 상태이상 칩(A) · 메시지 로그(E).
    /// 셋 다 이미 있던 데이터를 화면에 꺼내는 일이지 새 시스템이 아니다:
    /// 층 목록은 <see cref="DungeonLayout"/>, 상태는 <see cref="StatusEffects"/>,
    /// 로그 줄은 <c>InteractionFeedback</c>이 이미 턴마다 쏘고 있었고 3초 뒤 버려질 뿐이었다.
    /// </summary>
    public partial class PrototypeHudController : MonoBehaviour
    {
        /// <summary>로그에 남기는 줄 수. 좌하단 208×52 플레이트에 9px 네 줄이 정확히 든다.</summary>
        private const int MessageLogLines = 4;

        private readonly MessageLog _messages = new MessageLog(MessageLogLines);

        private VisualElement _statusChips;
        private VisualElement _floorStack;
        private VisualElement _floorTicks;
        private VisualElement _messageLogRoot;
        private VisualElement _feedbackChip;

        /// <summary>마지막으로 그린 상태이상 조합. 같으면 요소를 다시 만들지 않는다.</summary>
        private int _statusChipSignature = -1;

        private void BindReadouts(VisualElement root)
        {
            _statusChips = root.Q<VisualElement>("status-chips");
            _floorStack = root.Q<VisualElement>("floor-stack");
            _floorTicks = root.Q<VisualElement>(className: "floor-ticks");
            _messageLogRoot = root.Q<VisualElement>("message-log");
            _feedbackChip = root.Q<VisualElement>("feedback-chip");
            _statusChipSignature = -1;
        }

        // ── D · 층 스택 ─────────────────────────────────────

        /// <summary>
        /// 층 눈금을 다시 깐다. 세로 배치의 기준은 <b>공간 인덱스</b>(FloorIndex)다 —
        /// 진행 인덱스로 깔면 하강 던전에서 스택이 위아래로 뒤집힌다. 밝기의 기준만
        /// 진행 인덱스다("여기까지 가 봤나"는 공간이 아니라 진행의 질문이라서).
        /// </summary>
        private void RebuildFloorStack()
        {
            if (_floorStack == null || _floorTicks == null) return;

            // depth-label 은 현재 층 행으로 옮겨 다닌다. Clear 전에 스택으로 회수해 두면
            // 던전이 없어 조기 반환하는 경로에서도 트리 밖으로 떨어지지 않는다.
            if (_depthLabel != null && _depthLabel.parent != _floorStack)
                _floorStack.Add(_depthLabel);
            _floorTicks.Clear();

            List<DungeonFloorInfo> floors = SortedFloorsTopFirst();
            // 한 층짜리 던전(허브 포함)에서는 스택이 정보를 주지 못한다.
            bool hasStack = floors != null && floors.Count > 1;
            _floorStack.EnableInClassList("is-open", hasStack);
            if (!hasStack)
            {
                if (_floorLabel != null) _floorLabel.text = "";
                return;
            }

            int active = demo.ActiveProgressIndex;
            int furthest = demo.FurthestProgressIndex;

            foreach (DungeonFloorInfo floor in floors)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("floor-tick-row");

                var tick = new VisualElement { pickingMode = PickingMode.Ignore };
                tick.AddToClassList("floor-tick");
                bool current = floor.ProgressIndex == active;
                if (current) tick.AddToClassList("is-current");
                else if (floor.ProgressIndex <= furthest) tick.AddToClassList("is-explored");
                row.Add(tick);

                if (current && _depthLabel != null) row.Add(_depthLabel);
                _floorTicks.Add(row);
            }

            // 캡은 스택의 **양 끝**을 가리킨다 — 바로 위/아래 층이 아니다.
            // 캡이 인접 층을 가리키면 스택 꼭대기에 "B1"이 붙어서, 눈금이 말하는
            // 공간 순서와 글자가 말하는 순서가 어긋난다(실측 캡처에서 그렇게 나왔다).
            // 끝에 서 있으면 그 방향 캡은 나오지 않는다 — 갈 곳이 없다는 뜻이다.
            int activeFloor = demo.ActiveFloorIndex;
            SetFloorCap(_floorLabel, "▲ ", floors[0], activeFloor);

            var downCap = new Label { pickingMode = PickingMode.Ignore };
            downCap.AddToClassList("floor-cap");
            downCap.AddToClassList("is-down");
            SetFloorCap(downCap, "▼ ", floors[floors.Count - 1], activeFloor);
            _floorTicks.Add(downCap);
        }

        private void SetFloorCap(
            Label cap, string arrow, DungeonFloorInfo edge, int activeFloorIndex)
        {
            if (cap == null) return;

            bool standingOnEdge = edge.FloorIndex == activeFloorIndex;
            cap.text = standingOnEdge ? "" : arrow + demo.FloorLabel(edge.FloorIndex);
            cap.style.display = standingOnEdge ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>층을 위에서 아래 순서로. 상승·하강 던전 모두 같은 코드가 성립한다.</summary>
        private List<DungeonFloorInfo> SortedFloorsTopFirst()
        {
            if (demo == null) return null;
            IReadOnlyList<DungeonFloorInfo> source = demo.FloorProgression;
            if (source == null || source.Count == 0) return null;

            var floors = new List<DungeonFloorInfo>(source);
            floors.Sort((a, b) => b.FloorIndex.CompareTo(a.FloorIndex));
            return floors;
        }

        // ── A · 상태이상 칩 ─────────────────────────────────

        /// <summary>
        /// 활성 상태이상을 칩으로 그린다. 상태는 턴마다 바뀌는데 전용 이벤트가 없어
        /// 프레임 폴링하되, 조합이 그대로면 요소를 만들지 않는다(평상시 비용 0).
        /// </summary>
        private void UpdateStatusChips()
        {
            if (_statusChips == null) return;

            StatusEffects statuses = demo != null && demo.PlayerState != null
                ? demo.PlayerState.Statuses
                : null;
            int burn = statuses != null ? statuses.RemainingTurns(StatusKind.Burn) : 0;
            int freeze = statuses != null ? statuses.RemainingTurns(StatusKind.Freeze) : 0;
            int poison = statuses != null ? statuses.RemainingTurns(StatusKind.Poison) : 0;
            bool hungerWarning = demo != null && demo.HungerIsWarning;

            int signature = (burn & 0xFF) | ((freeze & 0xFF) << 8) | ((poison & 0xFF) << 16) |
                            (hungerWarning ? 1 << 24 : 0);
            if (signature == _statusChipSignature) return;
            _statusChipSignature = signature;

            // hunger-label 은 계약이 요구하는 이름이라 지우지 않는다 — 앞에 붙인 칩만 걷는다.
            for (int i = _statusChips.childCount - 1; i >= 0; i--)
                if (_statusChips[i] != _hungerLabel) _statusChips.RemoveAt(i);

            int slot = 0;
            InsertStatusChip(ref slot, burn, "pc-chip--burn", "화상");
            InsertStatusChip(ref slot, freeze, "pc-chip--freeze", "빙결");
            InsertStatusChip(ref slot, poison, "pc-chip--poison", "중독");

            // 포만은 95% 시간 동안 정보가 없다. 경고 이상일 때만 네 번째 칩으로 합류한다.
            if (_hungerLabel != null)
                _hungerLabel.style.display =
                    hungerWarning ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void InsertStatusChip(ref int slot, int turns, string variantClass, string label)
        {
            if (turns <= 0) return;

            var chip = new Label($"{label} {turns}") { pickingMode = PickingMode.Ignore };
            chip.AddToClassList("pc-chip");
            chip.AddToClassList(variantClass);
            _statusChips.Insert(slot++, chip);
        }

        // ── E · 메시지 로그 ─────────────────────────────────

        /// <summary>
        /// 로그를 다시 그린다. 최신 줄은 <c>#feedback-chip</c> 안의 <c>#status-label</c>이고
        /// (그래서 3초 강조가 그대로 산다) 지난 줄은 그 앞에 <c>.message-line</c>으로 깔린다.
        /// </summary>
        private void RebuildMessageLog()
        {
            if (_messageLogRoot == null) return;

            IReadOnlyList<string> lines = _messages.Lines();
            _messageLogRoot.EnableInClassList("is-open", lines.Count > 0);
            if (lines.Count == 0) return;

            for (int i = _messageLogRoot.childCount - 1; i >= 0; i--)
                if (_messageLogRoot[i] != _feedbackChip) _messageLogRoot.RemoveAt(i);

            for (int i = 0; i < lines.Count - 1; i++)
            {
                var line = new Label(lines[i]) { pickingMode = PickingMode.Ignore };
                line.AddToClassList("message-line");
                _messageLogRoot.Insert(i, line);
            }

            if (_statusLabel != null) _statusLabel.text = lines[lines.Count - 1];
        }
    }
}
