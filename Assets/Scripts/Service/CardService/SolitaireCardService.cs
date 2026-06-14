using System.Linq;
using Model.Card;
using Model.Game;

namespace Service.CardService
{
    /// <summary>
    /// Single ICardService implementation that handles all solitaire game types.
    /// Game-type-specific branching is delegated to <see cref="IDealRule"/>,
    /// so no modification or new class is needed when a new game type is added.
    /// </summary>
    public class SolitaireCardService : SolitaireCardServiceBase
    {
        protected override MoveCardResult ValidatePlacement(MoveCardRequest request, TableState state)
        {
            var targetPile = GetPile(state, request.TargetPileId);
            if (targetPile == null)
                return MoveCardResult.Fail("Target pile not found.");

            // Auto-collect games: foundations are write-only run sinks — no manual drops in or out.
            if (Rule.AutoCollectCompletedRuns
                && (request.TargetPileId.Type == PileType.Foundation
                    || request.SourcePileId.Type == PileType.Foundation))
                return MoveCardResult.Fail("Completed runs are collected automatically.");

            return targetPile.Id.Type switch
            {
                PileType.Tableau => ValidateTableauPlacement(request, state, targetPile),
                PileType.Foundation => request.Count == 1
                    ? ValidateFoundationPlacement(request.Card, targetPile)
                    : MoveCardResult.Fail("Only one card can be moved to the foundation at a time."),
                _ => MoveCardResult.Fail("Cannot move card to this pile type.")
            };
        }

        private MoveCardResult ValidateTableauPlacement(MoveCardRequest request, TableState state, PileState target)
        {
            // Multi-card moves must themselves form an alternating-color descending run.
            // (For Count == 1 the inner loop is a no-op.) Validated against the source pile
            // so the contract holds regardless of how the request was constructed.
            if (request.Count > 1)
            {
                var source = GetPile(state, request.SourcePileId);
                if (source == null)
                    return MoveCardResult.Fail("Source pile not found.");
                if (request.SourceIndex < 0 || request.SourceIndex + request.Count > source.Cards.Count)
                    return MoveCardResult.Fail("Source range is out of bounds.");

                for (int i = request.SourceIndex; i < request.SourceIndex + request.Count - 1; i++)
                {
                    var bottom = source.Cards[i];     // deeper in the pile, higher rank
                    var top = source.Cards[i + 1];    // shallower, one rank lower
                    if (Rule.RunRule == TableauRunRule.SameSuit)
                    {
                        if (top.Suit != bottom.Suit)
                            return MoveCardResult.Fail("Moved cards must share one suit.");
                    }
                    else if (!IsOppositeColor(bottom.Suit, top.Suit))
                    {
                        return MoveCardResult.Fail("Moved cards must alternate colors.");
                    }
                    if ((int)top.Rank != (int)bottom.Rank - 1)
                        return MoveCardResult.Fail("Moved cards must be in descending order.");
                }
            }

            if (target.Cards.Count == 0)
            {
                if (!Rule.OnlyKingOnEmptyTableau) return MoveCardResult.Success();
                return request.Card.Rank == Rank.King
                    ? MoveCardResult.Success()
                    : MoveCardResult.Fail("Only Kings can be placed on empty columns.");
            }

            var topCard = target.Cards[target.Cards.Count - 1];
            if (Rule.DropRule == TableauDropRule.AlternatingColor
                && !IsOppositeColor(request.Card.Suit, topCard.Suit))
                return MoveCardResult.Fail("Cards must alternate colors.");
            if ((int)request.Card.Rank != (int)topCard.Rank - 1)
                return MoveCardResult.Fail("Cards must be placed in descending order.");

            return MoveCardResult.Success();
        }

        private static MoveCardResult ValidateFoundationPlacement(PlayingCard card, PileState target)
        {
            if (target.Cards.Count == 0)
                return card.Rank == Rank.Ace
                    ? MoveCardResult.Success()
                    : MoveCardResult.Fail("Foundation must start with an Ace.");

            var topCard = target.Cards[target.Cards.Count - 1];
            if (card.Suit != topCard.Suit)
                return MoveCardResult.Fail("Foundation cards must be the same suit.");
            if (!IsFoundationSequence(topCard.Rank, card.Rank))
                return MoveCardResult.Fail("Foundation cards must be placed in ascending order.");

            return MoveCardResult.Success();
        }

        private static bool IsFoundationSequence(Rank top, Rank moving) =>
            (int)moving == (int)top + 1;

        // IsOppositeColor is inherited from SolitaireCardServiceBase (shared with IsValidRunPickup).

        private static PileState GetPile(TableState state, PileId id) =>
            state.Stock.Id.Equals(id) ? state.Stock :
            state.Waste.Id.Equals(id) ? state.Waste :
            state.Foundations.FirstOrDefault(p => p.Id.Equals(id)) ??
            state.Tableaus.FirstOrDefault(p => p.Id.Equals(id));
    }
}
