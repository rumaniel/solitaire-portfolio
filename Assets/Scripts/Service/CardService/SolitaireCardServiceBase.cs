using System.Collections.Generic;
using Model.Card;
using Model.Game;
using Service.GameService;

namespace Service.CardService
{
    public abstract class SolitaireCardServiceBase : ICardService
    {
        protected IDealRule Rule { get; private set; }

        public void Initialize(IDealRule rule)
        {
            Rule = rule;
        }

        public MoveCardResult TryMove(MoveCardRequest request, TableState state)
        {
            if (request.Card == null)
                return MoveCardResult.Fail("Cannot move a null card.");

            if (request.SourcePileId.Equals(request.TargetPileId))
                return MoveCardResult.Fail("Source and target piles are the same.");

            return ValidatePlacement(request, state);
        }

        public bool IsValidRunPickup(IReadOnlyList<PlayingCard> run)
        {
            // A drag can race the async game init (input exists before Initialize completes);
            // without a rule there is no legal run, so veto.
            if (Rule == null) return false;
            for (int i = 0; i < run.Count - 1; i++)
            {
                var bottom = run[i];      // deeper in the pile, higher rank
                var top = run[i + 1];     // shallower, one rank lower
                if ((int)top.Rank != (int)bottom.Rank - 1) return false;
                if (Rule.RunRule == TableauRunRule.SameSuit)
                {
                    if (top.Suit != bottom.Suit) return false;
                }
                else if (!IsOppositeColor(bottom.Suit, top.Suit))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>True when the two suits are of opposite color (one red, one black).</summary>
        protected static bool IsOppositeColor(Suit a, Suit b)
        {
            bool aIsRed = a == Suit.Heart || a == Suit.Diamond;
            bool bIsRed = b == Suit.Heart || b == Suit.Diamond;
            return aIsRed != bIsRed;
        }

        protected abstract MoveCardResult ValidatePlacement(MoveCardRequest request, TableState state);
    }
}
