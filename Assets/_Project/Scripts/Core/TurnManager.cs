namespace ProjectC.Core
{
    public enum TurnPhase
    {
        Player = 0,
        Enemies = 1
    }

    /// <summary>플레이어 행동 하나와 적 전체 행동을 한 턴으로 묶는 순수 C# 상태 머신.</summary>
    public sealed class TurnManager
    {
        public int TurnNumber { get; private set; } = 1;
        public TurnPhase Phase { get; private set; } = TurnPhase.Player;

        public bool TryBeginEnemyPhase()
        {
            if (Phase != TurnPhase.Player) return false;
            Phase = TurnPhase.Enemies;
            return true;
        }

        public bool TryCompleteEnemyPhase()
        {
            if (Phase != TurnPhase.Enemies) return false;
            TurnNumber++;
            Phase = TurnPhase.Player;
            return true;
        }

        public void Reset()
        {
            TurnNumber = 1;
            Phase = TurnPhase.Player;
        }
    }
}
