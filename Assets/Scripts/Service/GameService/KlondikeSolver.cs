#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Model.Card;
using Model.Game;
using Service.CardService;
using Service.HintService;

namespace Service.GameService
{
    /// <summary>Editor-only depth-first Klondike solver with state-key memoization, used by test sampling.</summary>
    public static class KlondikeSolver
    {
        public readonly struct SolveResult
        {
            public readonly bool Solved;
            public readonly int StatesExplored;
            public readonly bool BudgetExceeded;

            public SolveResult(bool solved, int statesExplored, bool budgetExceeded)
            {
                Solved = solved;
                StatesExplored = statesExplored;
                BudgetExceeded = budgetExceeded;
            }
        }

        public static SolveResult Solve(
            TableState initial,
            ICardService cardService,
            IDealRule rule,
            int stateBudget = 200_000)
        {
            var visited = new HashSet<ulong>();
            var stack = new Stack<TableState>();
            stack.Push(initial);
            visited.Add(StateHash(initial));

            while (stack.Count > 0)
            {
                if (visited.Count > stateBudget)
                    return new SolveResult(false, visited.Count, true);

                var state = stack.Pop();
                if (IsSolved(state, rule))
                    return new SolveResult(true, visited.Count, false);

                // Sort ascending + push to LIFO stack → highest priority is popped first
                // on the next iteration, so foundation-bound moves are explored before fillers.
                var moves = MoveEnumerator.FindAllMoves(state, cardService, rule);
                moves.Sort((a, b) => a.Priority.CompareTo(b.Priority));

                foreach (var hintMove in moves)
                {
                    TableState next;
                    if (hintMove.MoveType == HintMoveType.StockDraw)
                        next = ApplyStockDraw(state, rule);
                    else
                        next = ApplyMove(state, hintMove.Request);

                    var key = StateHash(next);
                    if (visited.Contains(key)) continue;
                    visited.Add(key);
                    stack.Push(next);
                }
            }

            return new SolveResult(false, visited.Count, false);
        }

        public static bool IsSolved(TableState state, IDealRule rule)
        {
            int target = rule.PerSuitCardCount;
            foreach (var foundation in state.Foundations)
                if (foundation.Cards.Count != target) return false;
            return true;
        }

        public static TableState ApplyMove(TableState state, MoveCardRequest request)
        {
            var source = FindPile(state, request.SourcePileId);
            var target = FindPile(state, request.TargetPileId);
            if (source == null || target == null)
                throw new InvalidOperationException($"Pile not found: {request.SourcePileId} or {request.TargetPileId}");

            var moved = new List<PlayingCard>();
            for (int i = request.SourceIndex; i < request.SourceIndex + request.Count; i++)
                moved.Add(source.Cards[i]);

            var updatedSource = RemoveCardsFrom(source, request.SourceIndex);
            var updatedTarget = AppendCards(target, moved);

            int newFanCount = state.WasteFanCount;
            if (request.SourcePileId.Type == PileType.Waste)
                newFanCount = Math.Max(0, newFanCount - request.Count);

            var replaced = ReplacePile(state, updatedSource);
            return ReplacePile(replaced, updatedTarget, newFanCount);
        }

        public static TableState ApplyStockDraw(TableState state, IDealRule rule)
        {
            var stock = state.Stock;
            var waste = state.Waste;

            PileState newStock, newWaste;
            int newFanCount;
            if (stock.Cards.Count == 0)
            {
                if (!rule.CanRecycleStock)
                    throw new InvalidOperationException("Cannot recycle stock: rule disallows.");
                var recycled = new List<PlayingCard>(waste.Cards);
                recycled.Reverse();
                newStock = new PileState(stock.Id, recycled, recycled.Count);
                newWaste = new PileState(waste.Id, new List<PlayingCard>(), 0);
                newFanCount = 0;
            }
            else
            {
                int drawCount = Math.Min(rule.StockDrawCount, stock.Cards.Count);
                var newStockCards = new List<PlayingCard>(stock.Cards);
                var newWasteCards = new List<PlayingCard>(waste.Cards);
                for (int i = 0; i < drawCount; i++)
                {
                    var top = newStockCards[newStockCards.Count - 1];
                    newStockCards.RemoveAt(newStockCards.Count - 1);
                    newWasteCards.Add(top);
                }
                newStock = new PileState(stock.Id, newStockCards, newStockCards.Count);
                newWaste = new PileState(waste.Id, newWasteCards, 0);
                newFanCount = drawCount;
            }

            var replaced = ReplacePile(state, newStock);
            return ReplacePile(replaced, newWaste, newFanCount);
        }

        private static PileState FindPile(TableState state, PileId id)
        {
            if (state.Stock.Id.Equals(id)) return state.Stock;
            if (state.Waste.Id.Equals(id)) return state.Waste;
            foreach (var f in state.Foundations) if (f.Id.Equals(id)) return f;
            foreach (var t in state.Tableaus) if (t.Id.Equals(id)) return t;
            return null;
        }

        private static PileState RemoveCardsFrom(PileState pile, int fromIndex)
        {
            var cards = new List<PlayingCard>(fromIndex);
            for (int i = 0; i < fromIndex; i++) cards.Add(pile.Cards[i]);

            var faceUpFrom = pile.FaceUpFromIndex;
            if (cards.Count > 0 && faceUpFrom >= cards.Count)
                faceUpFrom = cards.Count - 1;
            else if (cards.Count == 0)
                faceUpFrom = 0;

            return new PileState(pile.Id, cards, faceUpFrom);
        }

        private static PileState AppendCards(PileState pile, List<PlayingCard> newCards)
        {
            var cards = new List<PlayingCard>(pile.Cards.Count + newCards.Count);
            for (int i = 0; i < pile.Cards.Count; i++) cards.Add(pile.Cards[i]);
            cards.AddRange(newCards);
            return new PileState(pile.Id, cards, pile.FaceUpFromIndex);
        }

        private static TableState ReplacePile(TableState state, PileState updated, int? wasteFanCountOverride = null)
        {
            var stock = state.Stock.Id.Equals(updated.Id) ? updated : state.Stock;
            var waste = state.Waste.Id.Equals(updated.Id) ? updated : state.Waste;

            var foundations = new List<PileState>(state.Foundations.Count);
            foreach (var f in state.Foundations)
                foundations.Add(f.Id.Equals(updated.Id) ? updated : f);

            var tableaus = new List<PileState>(state.Tableaus.Count);
            foreach (var t in state.Tableaus)
                tableaus.Add(t.Id.Equals(updated.Id) ? updated : t);

            return new TableState(stock, waste, foundations, tableaus,
                wasteFanCountOverride ?? state.WasteFanCount);
        }

        // FNV-1a 64-bit over the canonical state stream. A 64-bit digest replaces the
        // previous string key: zero allocation per state and O(1) HashSet probes.
        // Collision odds at the 200k-state budget are ~2e-9 (birthday bound), and a
        // collision only over-prunes one branch — worst case a solvable deal reports
        // unsolvable and the caller resamples, never a false "solvable".
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
        private const byte PileSeparator = 0xFF;

        private static ulong StateHash(TableState state)
        {
            ulong h = FnvOffsetBasis;
            for (int i = 0; i < state.Tableaus.Count; i++)
            {
                var pile = state.Tableaus[i];
                h = Fnv(h, (byte)pile.FaceUpFromIndex);
                foreach (var c in pile.Cards)
                    h = Fnv(h, EncodeCard(c));
                h = Fnv(h, PileSeparator);
            }
            foreach (var c in state.Stock.Cards) h = Fnv(h, EncodeCard(c));
            h = Fnv(h, PileSeparator);
            foreach (var c in state.Waste.Cards) h = Fnv(h, EncodeCard(c));
            h = Fnv(h, PileSeparator);
            // Foundations: suit-of-bottom + count. (Suit×count uniquely identifies
            // the pile since foundations stack A→K of one suit; encoding count
            // alone would collide across different suit assignments.)
            for (int i = 0; i < state.Foundations.Count; i++)
            {
                var pile = state.Foundations[i];
                int count = pile.Cards.Count;
                int suitBits = count > 0 ? (int)pile.Cards[0].Suit : 0;
                h = Fnv(h, (byte)(((count & 0x1F) << 3) | (suitBits & 0x07)));
            }
            return h;
        }

        private static ulong Fnv(ulong hash, byte value) => (hash ^ value) * FnvPrime;

        private static byte EncodeCard(PlayingCard c)
            => (byte)(((int)c.Rank << 3) | (int)c.Suit);
    }
}
#endif
