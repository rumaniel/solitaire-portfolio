using System.Collections.Generic;
using System.Linq;
using Model.Card;
using Model.Game;
using Service.CardService;
using Service.GameService;

namespace Service.HintService
{
    public class HintService : IHintService
    {
        private readonly ICardService cardService;
        private IDealRule dealRule;

        public HintService(ICardService cardService)
        {
            this.cardService = cardService;
        }

        public void Initialize(IDealRule dealRule)
        {
            this.dealRule = dealRule;
        }

        public IReadOnlyList<HintMove> GetHints(TableState state)
        {
            var moves = MoveEnumerator.FindAllMoves(state, cardService, dealRule);
            // Strict total ordering — deterministic across platforms/runs
            moves.Sort((a, b) =>
            {
                int cmp = b.Priority.CompareTo(a.Priority);
                if (cmp != 0) return cmp;
                cmp = ((int)a.MoveType).CompareTo((int)b.MoveType);
                if (cmp != 0) return cmp;
                cmp = ((int)a.Request.SourcePileId.Type).CompareTo((int)b.Request.SourcePileId.Type);
                if (cmp != 0) return cmp;
                cmp = a.Request.SourcePileId.Index.CompareTo(b.Request.SourcePileId.Index);
                if (cmp != 0) return cmp;
                cmp = a.Request.SourceIndex.CompareTo(b.Request.SourceIndex);
                if (cmp != 0) return cmp;
                cmp = ((int)a.Request.TargetPileId.Type).CompareTo((int)b.Request.TargetPileId.Type);
                if (cmp != 0) return cmp;
                return a.Request.TargetPileId.Index.CompareTo(b.Request.TargetPileId.Index);
            });
            return moves;
        }

        public bool HasAnyMove(TableState state)
        {
            return MoveEnumerator.HasAnyMove(state, cardService, dealRule);
        }

        public bool CanAutoComplete(TableState state)
        {
            // Auto-collect games finish themselves run by run; the Klondike endgame doesn't apply.
            if (dealRule.AutoCollectCompletedRuns) return false;

            // All conditions must hold:
            // 1. Stock is empty
            // 2. Waste is empty
            // 3. All tableau cards are face-up (FaceUpFromIndex == 0 or pile is empty)
            if (state.Stock.Cards.Count > 0) return false;
            if (state.Waste.Cards.Count > 0) return false;

            foreach (var tableau in state.Tableaus)
            {
                if (tableau.Cards.Count > 0 && tableau.FaceUpFromIndex > 0)
                    return false;
            }

            // Verify there's at least something to move (not already won)
            return state.Tableaus.Any(t => t.Cards.Count > 0);
        }

        public IReadOnlyList<HintMove> GetAutoCompleteMoves(TableState state)
        {
            var moves = new List<HintMove>();
            if (!CanAutoComplete(state)) return moves;

            // Track virtual pile tops for greedy simulation
            var tableauTops = new int[state.Tableaus.Count];
            for (int i = 0; i < state.Tableaus.Count; i++)
                tableauTops[i] = state.Tableaus[i].Cards.Count - 1;

            var foundationTops = new PlayingCard[state.Foundations.Count];
            for (int i = 0; i < state.Foundations.Count; i++)
            {
                var f = state.Foundations[i];
                foundationTops[i] = f.Cards.Count > 0 ? f.Cards[f.Cards.Count - 1] : null;
            }

            bool progress = true;
            while (progress)
            {
                progress = false;
                for (int t = 0; t < state.Tableaus.Count; t++)
                {
                    if (tableauTops[t] < 0) continue;

                    var tableau = state.Tableaus[t];
                    var card = tableau.Cards[tableauTops[t]];

                    for (int f = 0; f < state.Foundations.Count; f++)
                    {
                        if (CanPlaceOnFoundation(card, foundationTops[f]))
                        {
                            var req = new MoveCardRequest(
                                card, tableau.Id, tableauTops[t], state.Foundations[f].Id);
                            moves.Add(new HintMove(req, 100, HintMoveType.TableauToFoundation));

                            tableauTops[t]--;
                            foundationTops[f] = card;
                            progress = true;
                            break;
                        }
                    }
                }
            }

            return moves;
        }

        private static bool CanPlaceOnFoundation(PlayingCard card, PlayingCard foundationTop)
        {
            if (foundationTop == null)
                return card.Rank == Rank.Ace;

            return card.Suit == foundationTop.Suit
                && (int)card.Rank == (int)foundationTop.Rank + 1;
        }
    }
}
