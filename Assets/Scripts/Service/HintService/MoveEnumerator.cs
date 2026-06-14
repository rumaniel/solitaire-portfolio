using System.Collections.Generic;
using Model.Card;
using Model.Game;
using Service.CardService;
using Service.GameService;

namespace Service.HintService
{
    /// <summary>
    /// Pure static class that enumerates all valid moves from a given <see cref="TableState"/>.
    /// Reuses <see cref="ICardService.TryMove"/> for validation — no rule duplication.
    /// Worst-case ~362 TryMove calls; each is pure field comparison, well under 1 ms on mobile.
    /// </summary>
    public static class MoveEnumerator
    {
        /// <summary>
        /// Returns all valid moves from the current state, each scored by strategic priority.
        /// </summary>
        public static List<HintMove> FindAllMoves(
            TableState state, ICardService cardService, IDealRule dealRule)
        {
            var moves = new List<HintMove>();

            AddWasteMoves(state, cardService, moves);
            AddTableauToFoundationMoves(state, cardService, moves);
            AddTableauToTableauMoves(state, cardService, moves);
            AddFoundationToTableauMoves(state, cardService, moves);
            AddStockDrawMove(state, dealRule, moves);

            return moves;
        }

        /// <summary>
        /// Returns true as soon as any valid move is found (short-circuit).
        /// Checks cheapest sources first for performance.
        /// </summary>
        public static bool HasAnyMove(
            TableState state, ICardService cardService, IDealRule dealRule)
        {
            // Stock draw is cheapest to check — field comparison only
            if (HasStockDraw(state, dealRule))
                return true;

            // Waste moves
            if (state.Waste.Cards.Count > 0)
            {
                var wasteCard = state.Waste.Cards[state.Waste.Cards.Count - 1];
                int wasteIndex = state.Waste.Cards.Count - 1;

                foreach (var foundation in state.Foundations)
                {
                    var req = new MoveCardRequest(wasteCard, state.Waste.Id, wasteIndex, foundation.Id);
                    if (cardService.TryMove(req, state).IsSuccess) return true;
                }

                foreach (var tableau in state.Tableaus)
                {
                    var req = new MoveCardRequest(wasteCard, state.Waste.Id, wasteIndex, tableau.Id);
                    if (cardService.TryMove(req, state).IsSuccess) return true;
                }
            }

            // Tableau to foundation
            foreach (var source in state.Tableaus)
            {
                if (source.Cards.Count == 0) continue;
                var topCard = source.Cards[source.Cards.Count - 1];
                int topIndex = source.Cards.Count - 1;

                foreach (var foundation in state.Foundations)
                {
                    var req = new MoveCardRequest(topCard, source.Id, topIndex, foundation.Id);
                    if (cardService.TryMove(req, state).IsSuccess) return true;
                }
            }

            // Tableau to tableau (single and stack)
            for (int s = 0; s < state.Tableaus.Count; s++)
            {
                var source = state.Tableaus[s];
                if (source.Cards.Count == 0) continue;

                for (int i = source.FaceUpFromIndex; i < source.Cards.Count; i++)
                {
                    var card = source.Cards[i];
                    int count = source.Cards.Count - i;

                    for (int t = 0; t < state.Tableaus.Count; t++)
                    {
                        if (t == s) continue;
                        var target = state.Tableaus[t];

                        if (IsUselessKingMove(source, i, target)) continue;

                        var req = new MoveCardRequest(card, source.Id, i, target.Id, count);
                        if (cardService.TryMove(req, state).IsSuccess) return true;
                    }
                }
            }

            // Foundation to tableau
            foreach (var foundation in state.Foundations)
            {
                if (foundation.Cards.Count == 0) continue;
                var topCard = foundation.Cards[foundation.Cards.Count - 1];
                int topIndex = foundation.Cards.Count - 1;

                foreach (var tableau in state.Tableaus)
                {
                    var req = new MoveCardRequest(topCard, foundation.Id, topIndex, tableau.Id);
                    if (cardService.TryMove(req, state).IsSuccess) return true;
                }
            }

            return false;
        }

        private static void AddWasteMoves(
            TableState state, ICardService cardService, List<HintMove> moves)
        {
            if (state.Waste.Cards.Count == 0) return;

            var wasteCard = state.Waste.Cards[state.Waste.Cards.Count - 1];
            int wasteIndex = state.Waste.Cards.Count - 1;

            // Waste to foundation
            foreach (var foundation in state.Foundations)
            {
                var req = new MoveCardRequest(wasteCard, state.Waste.Id, wasteIndex, foundation.Id);
                if (cardService.TryMove(req, state).IsSuccess)
                    moves.Add(new HintMove(req, 90, HintMoveType.WasteToFoundation));
            }

            // Waste to tableau
            foreach (var tableau in state.Tableaus)
            {
                var req = new MoveCardRequest(wasteCard, state.Waste.Id, wasteIndex, tableau.Id);
                if (cardService.TryMove(req, state).IsSuccess)
                    moves.Add(new HintMove(req, 50, HintMoveType.WasteToTableau));
            }
        }

        private static void AddTableauToFoundationMoves(
            TableState state, ICardService cardService, List<HintMove> moves)
        {
            foreach (var source in state.Tableaus)
            {
                if (source.Cards.Count == 0) continue;
                var topCard = source.Cards[source.Cards.Count - 1];
                int topIndex = source.Cards.Count - 1;

                foreach (var foundation in state.Foundations)
                {
                    var req = new MoveCardRequest(topCard, source.Id, topIndex, foundation.Id);
                    if (cardService.TryMove(req, state).IsSuccess)
                    {
                        bool reveals = WouldReveal(source, topIndex);
                        int priority = reveals ? 120 : 100;
                        moves.Add(new HintMove(req, priority, HintMoveType.TableauToFoundation));
                    }
                }
            }
        }

        private static void AddTableauToTableauMoves(
            TableState state, ICardService cardService, List<HintMove> moves)
        {
            for (int s = 0; s < state.Tableaus.Count; s++)
            {
                var source = state.Tableaus[s];
                if (source.Cards.Count == 0) continue;

                for (int i = source.FaceUpFromIndex; i < source.Cards.Count; i++)
                {
                    var card = source.Cards[i];
                    int count = source.Cards.Count - i;

                    for (int t = 0; t < state.Tableaus.Count; t++)
                    {
                        if (t == s) continue;
                        var target = state.Tableaus[t];

                        if (IsUselessKingMove(source, i, target)) continue;

                        var req = new MoveCardRequest(card, source.Id, i, target.Id, count);
                        if (!cardService.TryMove(req, state).IsSuccess) continue;

                        bool reveals = WouldReveal(source, i);
                        if (reveals)
                            moves.Add(new HintMove(req, 80, HintMoveType.TableauToTableauReveal));
                        else
                            moves.Add(new HintMove(req, 20, HintMoveType.TableauToTableau));
                    }
                }
            }
        }

        private static void AddFoundationToTableauMoves(
            TableState state, ICardService cardService, List<HintMove> moves)
        {
            foreach (var foundation in state.Foundations)
            {
                if (foundation.Cards.Count == 0) continue;
                var topCard = foundation.Cards[foundation.Cards.Count - 1];
                int topIndex = foundation.Cards.Count - 1;

                foreach (var tableau in state.Tableaus)
                {
                    var req = new MoveCardRequest(topCard, foundation.Id, topIndex, tableau.Id);
                    if (cardService.TryMove(req, state).IsSuccess)
                        moves.Add(new HintMove(req, 5, HintMoveType.FoundationToTableau));
                }
            }
        }

        private static void AddStockDrawMove(
            TableState state, IDealRule dealRule, List<HintMove> moves)
        {
            if (HasStockDraw(state, dealRule))
                moves.Add(new HintMove(default, 10, HintMoveType.StockDraw));
        }

        private static bool HasStockDraw(TableState state, IDealRule dealRule)
        {
            if (state.Stock.Cards.Count > 0)
            {
                // Classic Spider guard: dealing is illegal while any column is empty,
                // so a stock hint would point at a rejected tap.
                if (dealRule.StockDealsToTableau && dealRule.StockDealRequiresNoEmptyColumn)
                {
                    foreach (var t in state.Tableaus)
                        if (t.Cards.Count == 0) return false;
                }
                return true;
            }
            return dealRule.CanRecycleStock && state.Waste.Cards.Count > 0;
        }

        /// <summary>
        /// Returns true if removing a card at the given source index would expose
        /// a face-down card beneath it (i.e., cause a tableau reveal).
        /// </summary>
        private static bool WouldReveal(PileState source, int sourceIndex)
        {
            return sourceIndex > 0 && !source.IsFaceUp(sourceIndex - 1);
        }

        /// <summary>
        /// Prunes useless moves where a King (or King-led stack) that occupies
        /// the entire face-up portion of a pile is moved to another empty tableau.
        /// This is a no-op reorganization that wastes a hint slot.
        /// </summary>
        private static bool IsUselessKingMove(PileState source, int sourceIndex, PileState target)
        {
            // Only prune moves to empty targets
            if (target.Cards.Count > 0) return false;

            // Only prune if the moved card is a King and is the bottom-most face-up card
            if (source.Cards[sourceIndex].Rank != Rank.King) return false;
            if (sourceIndex != source.FaceUpFromIndex) return false;

            // If there are face-down cards below, the move reveals — not useless
            if (sourceIndex > 0) return false;

            // King stack occupies the entire pile, moving to empty is pointless
            return true;
        }
    }
}
