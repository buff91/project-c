namespace ProjectC.Core
{
    public enum VerticalRouteRole
    {
        LocalStairs,
        Ladder,
        FloorUp,
        FloorDown,
        OpeningUp,
        OpeningDown
    }

    /// <summary>
    /// 수직 이동 수단을 처음 발견했을 때 보여줄 짧은 설명.
    /// 색 이름이 아니라 "어떻게 생겼고 무엇을 하면 어디로 가는지"를 말한다.
    /// </summary>
    public readonly struct VerticalRouteCue
    {
        public VerticalRouteRole Role { get; }
        public string Title { get; }
        public string Detail { get; }
        public string WorldLabel { get; }

        private VerticalRouteCue(
            VerticalRouteRole role,
            string title,
            string detail,
            string worldLabel)
        {
            Role = role;
            Title = title;
            Detail = detail;
            WorldLabel = worldLabel;
        }

        public static bool TryCreate(
            TileKind kind,
            bool viewedFromBelow,
            string destinationLabel,
            out VerticalRouteCue cue)
        {
            string destination = string.IsNullOrEmpty(destinationLabel)
                ? "다른 높이"
                : destinationLabel;

            switch (kind)
            {
                case TileKind.Stairs:
                    cue = new VerticalRouteCue(
                        VerticalRouteRole.LocalStairs,
                        "발판 계단",
                        "계단이 바닥과 이어져 있다. 그대로 걸으면 높이가 바뀐다.",
                        "WALK");
                    return true;
                case TileKind.Ladder:
                    cue = new VerticalRouteCue(
                        VerticalRouteRole.Ladder,
                        "벽 사다리",
                        "사다리 발판에 선 뒤 캐릭터를 탭하거나 Space를 누르면 오른다.",
                        "CLIMB");
                    return true;
                case TileKind.StairsUp:
                    cue = new VerticalRouteCue(
                        VerticalRouteRole.FloorUp,
                        $"상층 계단 · {destination}",
                        "어두운 계단 입구를 밟으면 즉시 던전의 한 층 위로 이동한다.",
                        $"{destination} ▲");
                    return true;
                case TileKind.StairsDown:
                    cue = new VerticalRouteCue(
                        VerticalRouteRole.FloorDown,
                        $"하층 계단 · {destination}",
                        "어두운 계단 입구를 밟으면 즉시 던전의 한 층 아래로 이동한다.",
                        $"{destination} ▼");
                    return true;
                case TileKind.Hole:
                    cue = viewedFromBelow
                        ? new VerticalRouteCue(
                            VerticalRouteRole.OpeningUp,
                            $"천장 개구부 · {destination}",
                            "뚫린 천장 너머로 위층 일부를 올려다볼 수 있다.",
                            $"{destination} ▲")
                        : new VerticalRouteCue(
                            VerticalRouteRole.OpeningDown,
                            $"바닥 개구부 · {destination}",
                            "구멍 아래가 실제로 보인다. 구멍을 탭하면 아래층으로 뛰어내린다.",
                            $"{destination} ▼");
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }
    }
}
