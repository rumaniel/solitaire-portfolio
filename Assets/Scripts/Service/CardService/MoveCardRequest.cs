using Model.Card;
using Model.Game;

namespace Service.CardService
{
    public readonly struct MoveCardRequest
    {
        public PlayingCard Card { get; }
        public PileId SourcePileId { get; }
        public int SourceIndex { get; }
        public PileId TargetPileId { get; }
        /// <summary>Number of cards to move starting at SourceIndex. 1 for single-card moves.</summary>
        public int Count { get; }

        public MoveCardRequest(PlayingCard card, PileId sourcePileId, int sourceIndex, PileId targetPileId, int count = 1)
        {
            Card = card;
            SourcePileId = sourcePileId;
            SourceIndex = sourceIndex;
            TargetPileId = targetPileId;
            Count = count;
        }
    }
}
