using Model.Card;
using Model.Game;

namespace Component.Card.Events
{
    /// <summary>
    /// Raised when a dragged card is dropped on a target pile.
    /// </summary>
    public readonly struct CardDroppedOnPile
    {
        public UICard CardView { get; }
        public PlayingCard Card { get; }
        public PileId SourcePileId { get; }
        public int SourceIndex { get; }
        public PileId TargetPileId { get; }
        /// <summary>Number of cards in the dragged stack. 1 for single-card drops.</summary>
        public int Count { get; }

        public CardDroppedOnPile(
            UICard cardView,
            PlayingCard card,
            PileId sourcePileId,
            int sourceIndex,
            PileId targetPileId,
            int count = 1)
        {
            CardView = cardView;
            Card = card;
            SourcePileId = sourcePileId;
            SourceIndex = sourceIndex;
            TargetPileId = targetPileId;
            Count = count;
        }
    }
}
