using Model.Card;
using Model.Game;

namespace Component.Card.Events
{
    /// <summary>
    /// Raised when a card drag starts from a specific source pile and index.
    /// </summary>
    public readonly struct CardDragCanceled
    {
        public UICard CardView { get; }
        public PlayingCard Card { get; }
        public PileId SourcePileId { get; }
        public int SourceIndex { get; }

        public CardDragCanceled(UICard cardView, PlayingCard card, PileId sourcePileId, int sourceIndex)
        {
            CardView = cardView;
            Card = card;
            SourcePileId = sourcePileId;
            SourceIndex = sourceIndex;
        }
    }
}
