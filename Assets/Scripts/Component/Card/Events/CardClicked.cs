using Model.Card;
using Model.Game;

namespace Component.Card.Events
{
    /// <summary>
    /// Raised when a card is clicked (pointer click without drag).
    /// </summary>
    public readonly struct CardClicked
    {
        public UICard CardView { get; }
        public PlayingCard Card { get; }
        public PileId SourcePileId { get; }
        public int SourceIndex { get; }

        public CardClicked(UICard cardView, PlayingCard card, PileId sourcePileId, int sourceIndex)
        {
            CardView = cardView;
            Card = card;
            SourcePileId = sourcePileId;
            SourceIndex = sourceIndex;
        }
    }
}
