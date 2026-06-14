using Model.Game;

namespace Model.Stats
{
    public readonly struct ScoredMoveInfo
    {
        public MoveType MoveType { get; }
        public PileType SourcePileType { get; }
        public PileType TargetPileType { get; }
        public bool CausedTableauReveal { get; }

        public ScoredMoveInfo(
            MoveType moveType,
            PileType sourcePileType = PileType.None,
            PileType targetPileType = PileType.None,
            bool causedTableauReveal = false)
        {
            MoveType = moveType;
            SourcePileType = sourcePileType;
            TargetPileType = targetPileType;
            CausedTableauReveal = causedTableauReveal;
        }
    }
}
