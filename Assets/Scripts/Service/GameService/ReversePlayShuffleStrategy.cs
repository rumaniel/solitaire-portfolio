using System;
using System.Collections.Generic;
using Model.Card;
using Model.Game;

namespace Service.GameService
{
    /// <summary>
    /// Seed-deterministic Klondike deal generator built around a reverse-play loop:
    /// starts from a solved state (all 52 cards on foundations) and applies random
    /// reverse moves, then reshapes the result into the standard triangle deal.
    /// Phase 2's reshape synthesizes cards into empty/short columns; full winnability
    /// is not formally proven end-to-end (see PR #60 tests for empirical sampling).
    /// </summary>
    public class ReversePlayShuffleStrategy : IShuffleStrategy, IReversePlayStrategy
    {
        private readonly int reverseSteps;

        // 500 chosen empirically — at the previous default (200) the reverse-play history
        // was short enough that the visible face-up row often kept recognisable A-K runs
        // (a "sorted" feel). Adding more steps gives more T→W and FlipDown moves,
        // breaking those runs while preserving Phase-1 reversibility.
        public ReversePlayShuffleStrategy(int reverseSteps = 500)
        {
            this.reverseSteps = Math.Max(0, reverseSteps);
        }

        public TableState BuildInitialState(int seed, IDealRule dealRule)
        {
            ValidateRule(dealRule);

            var rng = new Random(seed);
            var ws = BuildSolvedWorkState(rng, dealRule);
            Phase1_ReverseScramble(ws, rng, dealRule);
            Phase2_FillRemainingColumns(ws, rng, dealRule);
            Phase3_FlushToStock(ws, rng);
            Phase4_NormalizeFaceUp(ws, dealRule);
            return ws.ToTableState(dealRule);
        }

        public List<PlayingCard> Shuffle(int seed)
        {
            // IShuffleStrategy fallback path — SolitaireGameService normally detects
            // IReversePlayStrategy and bypasses DealBuilder. Kept so provider swap is safe.
            var state = BuildInitialState(seed, new NullDealRuleForShuffle());
            return state.Linearize();
        }

        private static void ValidateRule(IDealRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (rule.InitialCardCounts == null)
                throw new ArgumentException("DealRule.InitialCardCounts must not be null.", nameof(rule));
            if (rule.InitialCardCounts.Length < rule.TableauCount)
                throw new ArgumentException(
                    $"DealRule.InitialCardCounts has {rule.InitialCardCounts.Length} entries " +
                    $"but TableauCount is {rule.TableauCount}.",
                    nameof(rule));
            if (rule.FoundationCount != 4 || rule.PerSuitCardCount != 13)
                throw new ArgumentException(
                    "ReversePlayShuffleStrategy only supports standard 4-suit × 13-rank Klondike rules.",
                    nameof(rule));
        }

        // ---- Phase 0: solved starting state ----

        private static WorkState BuildSolvedWorkState(Random rng, IDealRule rule)
        {
            var ws = new WorkState(rule.TableauCount, rule.FoundationCount);

            var suits = new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond };
            ShuffleArray(suits, rng);

            for (int f = 0; f < ws.Foundations.Length && f < suits.Length; f++)
            {
                for (int rank = (int)Rank.Ace; rank <= (int)Rank.King; rank++)
                    ws.Foundations[f].Add(new PlayingCard((Rank)rank, suits[f]));
            }

            return ws;
        }

        private static void ShuffleArray<T>(T[] items, Random rng)
        {
            for (int i = items.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        // ---- Phase 1: reverse-scramble ----

        private void Phase1_ReverseScramble(WorkState ws, Random rng, IDealRule rule)
            => Phase1_ReverseScramble(ws, rng, rule, null);

        private void Phase1_ReverseScramble(WorkState ws, Random rng, IDealRule rule, List<WeightedMove> trace)
        {
            var candidates = new List<WeightedMove>(64);
            for (int step = 0; step < reverseSteps; step++)
            {
                candidates.Clear();
                EnumerateReverseMoves(ws, rule, candidates);
                if (candidates.Count == 0) break;

                int totalWeight = 0;
                for (int i = 0; i < candidates.Count; i++) totalWeight += candidates[i].Weight;
                int roll = rng.Next(totalWeight);
                int cumulative = 0;
                for (int i = 0; i < candidates.Count; i++)
                {
                    cumulative += candidates[i].Weight;
                    if (roll < cumulative)
                    {
                        ApplyMove(ws, candidates[i]);
                        trace?.Add(candidates[i]);
                        break;
                    }
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: replays Phase 1 in reverse to verify each move was individually reversible.</summary>
        public bool ForTesting_VerifyPhase1Reversible(int seed, IDealRule dealRule)
        {
            ValidateRule(dealRule);

            var rng = new Random(seed);
            var ws = BuildSolvedWorkState(rng, dealRule);
            var trace = new List<WeightedMove>(reverseSteps);
            Phase1_ReverseScramble(ws, rng, dealRule, trace);

            for (int i = trace.Count - 1; i >= 0; i--)
                ApplyInverseMove(ws, trace[i]);

            return IsSolvedWorkState(ws);
        }

        private static void ApplyInverseMove(WorkState ws, WeightedMove move)
        {
            switch (move.Kind)
            {
                case ReverseMoveKind.FoundationToTableau:
                {
                    var pile = ws.Tableau[move.To];
                    var card = pile[pile.Count - 1];
                    pile.RemoveAt(pile.Count - 1);
                    ws.Foundations[move.From].Add(card);
                    break;
                }
                case ReverseMoveKind.FoundationToWaste:
                {
                    var card = ws.Waste[ws.Waste.Count - 1];
                    ws.Waste.RemoveAt(ws.Waste.Count - 1);
                    ws.Foundations[move.From].Add(card);
                    break;
                }
                case ReverseMoveKind.TableauToWaste:
                {
                    var card = ws.Waste[ws.Waste.Count - 1];
                    ws.Waste.RemoveAt(ws.Waste.Count - 1);
                    ws.Tableau[move.From].Add(card);
                    break;
                }
                case ReverseMoveKind.WasteToStock:
                {
                    var card = ws.Stock[ws.Stock.Count - 1];
                    ws.Stock.RemoveAt(ws.Stock.Count - 1);
                    ws.Waste.Add(card);
                    break;
                }
                case ReverseMoveKind.FlipDown:
                    ws.TableauFaceUpFrom[move.From] -= 1;
                    break;
            }
        }

        private static bool IsSolvedWorkState(WorkState ws)
        {
            if (ws.Waste.Count != 0 || ws.Stock.Count != 0) return false;
            foreach (var t in ws.Tableau) if (t.Count != 0) return false;

            foreach (var f in ws.Foundations)
            {
                if (f.Count != 13) return false;
                var suit = f[0].Suit;
                for (int i = 0; i < 13; i++)
                {
                    if (f[i].Suit != suit) return false;
                    if (f[i].Rank != (Rank)(i + 1)) return false;
                }
            }
            return true;
        }
#endif

        private static void EnumerateReverseMoves(WorkState ws, IDealRule rule, List<WeightedMove> output)
        {
            // F→T
            for (int fi = 0; fi < ws.Foundations.Length; fi++)
            {
                if (ws.Foundations[fi].Count == 0) continue;
                var card = ws.Foundations[fi][ws.Foundations[fi].Count - 1];
                for (int ti = 0; ti < ws.Tableau.Length; ti++)
                {
                    if (IsLegalTableauPlacement(card, ws.Tableau[ti], ws.TableauFaceUpFrom[ti], rule))
                        output.Add(new WeightedMove(ReverseMoveKind.FoundationToTableau, fi, ti, 40));
                }
            }

            // F→W
            for (int fi = 0; fi < ws.Foundations.Length; fi++)
            {
                if (ws.Foundations[fi].Count > 0)
                    output.Add(new WeightedMove(ReverseMoveKind.FoundationToWaste, fi, -1, 10));
            }

            // T→W — require face-up run ≥ 2 so the column always retains a face-up top
            for (int ti = 0; ti < ws.Tableau.Length; ti++)
            {
                int runLen = ws.Tableau[ti].Count - ws.TableauFaceUpFrom[ti];
                if (runLen >= 2)
                    output.Add(new WeightedMove(ReverseMoveKind.TableauToWaste, ti, -1, 20));
            }

            // W→S
            if (ws.Waste.Count > 0)
                output.Add(new WeightedMove(ReverseMoveKind.WasteToStock, -1, -1, 10));

            // Flip-down
            for (int ti = 0; ti < ws.Tableau.Length; ti++)
            {
                int runLen = ws.Tableau[ti].Count - ws.TableauFaceUpFrom[ti];
                if (runLen >= 2)
                    output.Add(new WeightedMove(ReverseMoveKind.FlipDown, ti, -1, 20));
            }
        }

        private static void ApplyMove(WorkState ws, WeightedMove move)
        {
            switch (move.Kind)
            {
                case ReverseMoveKind.FoundationToTableau:
                {
                    var foundation = ws.Foundations[move.From];
                    var card = foundation[foundation.Count - 1];
                    foundation.RemoveAt(foundation.Count - 1);
                    ws.Tableau[move.To].Add(card);
                    break;
                }
                case ReverseMoveKind.FoundationToWaste:
                {
                    var foundation = ws.Foundations[move.From];
                    ws.Waste.Add(foundation[foundation.Count - 1]);
                    foundation.RemoveAt(foundation.Count - 1);
                    break;
                }
                case ReverseMoveKind.TableauToWaste:
                {
                    var pile = ws.Tableau[move.From];
                    var card = pile[pile.Count - 1];
                    pile.RemoveAt(pile.Count - 1);
                    ws.Waste.Add(card);
                    // Defensive clamp — enumerator only emits T→W when runLen ≥ 2, so
                    // this branch should never fire, but we keep the top face-up either way.
                    if (ws.TableauFaceUpFrom[move.From] >= pile.Count && pile.Count > 0)
                        ws.TableauFaceUpFrom[move.From] = pile.Count - 1;
                    break;
                }
                case ReverseMoveKind.WasteToStock:
                {
                    var card = ws.Waste[ws.Waste.Count - 1];
                    ws.Waste.RemoveAt(ws.Waste.Count - 1);
                    ws.Stock.Add(card);
                    break;
                }
                case ReverseMoveKind.FlipDown:
                    ws.TableauFaceUpFrom[move.From] += 1;
                    break;
            }
        }

        private static bool IsLegalTableauPlacement(
            PlayingCard moving, List<PlayingCard> pileCards, int faceUpFromIndex, IDealRule rule)
        {
            if (pileCards.Count == 0)
                return !rule.OnlyKingOnEmptyTableau || moving.Rank == Rank.King;

            // Placement requires the current top to be face-up (mirrors forward-play legality).
            int topIdx = pileCards.Count - 1;
            if (topIdx < faceUpFromIndex) return false;

            var top = pileCards[topIdx];
            if (!IsOppositeColor(moving.Suit, top.Suit)) return false;
            return (int)moving.Rank == (int)top.Rank - 1;
        }

        private static bool IsOppositeColor(Suit a, Suit b)
        {
            bool aIsRed = a == Suit.Heart || a == Suit.Diamond;
            bool bIsRed = b == Suit.Heart || b == Suit.Diamond;
            return aIsRed != bIsRed;
        }

        private enum ReverseMoveKind
        {
            FoundationToTableau,
            FoundationToWaste,
            TableauToWaste,
            WasteToStock,
            FlipDown,
        }

        private readonly struct WeightedMove
        {
            public readonly ReverseMoveKind Kind;
            public readonly int From;
            public readonly int To;
            public readonly int Weight;

            public WeightedMove(ReverseMoveKind kind, int from, int to, int weight)
            {
                Kind = kind;
                From = from;
                To = to;
                Weight = weight;
            }
        }

        // ---- Phase 2: reshape tableau to triangle ----

        private void Phase2_FillRemainingColumns(WorkState ws, Random rng, IDealRule rule)
        {
            var pool = new List<PlayingCard>();
            for (int f = 0; f < ws.Foundations.Length; f++)
            {
                pool.AddRange(ws.Foundations[f]);
                ws.Foundations[f].Clear();
            }
            pool.AddRange(ws.Waste);
            ws.Waste.Clear();

            ShuffleList(pool, rng);

            // Pass 1 — truncate every over-long column from the bottom into stock.
            // Phase 1 seeds each column it touches with a K → Q → J anchor at index 0
            // (only Kings are legal on empty columns), so trimming from the top would
            // leave those anchors as the visible face-up card and bury the lower ranks
            // the player needs for foundation progress. Doing all truncations before
            // any fill also means a later donor column's overflow is in stock by the
            // time an earlier short column reaches the fallback fill below.
            for (int c = 0; c < ws.Tableau.Length; c++)
            {
                int target = rule.InitialCardCounts[c];
                var pile = ws.Tableau[c];
                if (pile.Count <= target) continue;

                int removeCount = pile.Count - target;
                for (int i = 0; i < removeCount; i++)
                    ws.Stock.Add(pile[i]);
                pile.RemoveRange(0, removeCount);
            }

            // Pass 2 — fill short columns. Pool cards are appended (face-up side) so
            // a random pool card surfaces as visible and any partial Phase-1 anchor
            // is demoted to face-down. Stock fallback covers the (rare) case where
            // the pool is exhausted before the triangle is complete.
            int poolCursor = 0;
            for (int c = 0; c < ws.Tableau.Length; c++)
            {
                int target = rule.InitialCardCounts[c];
                var pile = ws.Tableau[c];

                while (pile.Count < target && poolCursor < pool.Count)
                    pile.Add(pool[poolCursor++]);

                while (pile.Count < target && ws.Stock.Count > 0)
                {
                    pile.Add(ws.Stock[ws.Stock.Count - 1]);
                    ws.Stock.RemoveAt(ws.Stock.Count - 1);
                }

                if (ws.TableauFaceUpFrom[c] > pile.Count)
                    ws.TableauFaceUpFrom[c] = pile.Count;
            }

            while (poolCursor < pool.Count)
                ws.Stock.Add(pool[poolCursor++]);
        }

        private static void ShuffleList(List<PlayingCard> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ---- Phase 3: stock reshuffle ----

        private void Phase3_FlushToStock(WorkState ws, Random rng) => ShuffleList(ws.Stock, rng);

        // ---- Phase 4: face-up normalization ----

        private void Phase4_NormalizeFaceUp(WorkState ws, IDealRule rule)
        {
            int faceUpPerColumn = Math.Max(1, rule.InitialFaceUpPerColumn);
            for (int c = 0; c < ws.Tableau.Length; c++)
            {
                int count = ws.Tableau[c].Count;
                ws.TableauFaceUpFrom[c] = Math.Max(0, count - faceUpPerColumn);
            }
        }

        private class WorkState
        {
            public List<PlayingCard>[] Tableau { get; }
            public int[] TableauFaceUpFrom { get; }
            public List<PlayingCard> Stock { get; } = new();
            public List<PlayingCard> Waste { get; } = new();
            public List<PlayingCard>[] Foundations { get; }

            public WorkState(int tableauCount, int foundationCount)
            {
                Tableau = new List<PlayingCard>[tableauCount];
                TableauFaceUpFrom = new int[tableauCount];
                for (int i = 0; i < tableauCount; i++)
                {
                    Tableau[i] = new List<PlayingCard>();
                    TableauFaceUpFrom[i] = 0;
                }
                Foundations = new List<PlayingCard>[foundationCount];
                for (int i = 0; i < foundationCount; i++)
                    Foundations[i] = new List<PlayingCard>();
            }

            public TableState ToTableState(IDealRule dealRule)
            {
                var tableaus = new List<PileState>(Tableau.Length);
                for (int i = 0; i < Tableau.Length; i++)
                {
                    tableaus.Add(new PileState(
                        new PileId(PileType.Tableau, i),
                        new List<PlayingCard>(Tableau[i]),
                        TableauFaceUpFrom[i]));
                }

                var stock = new PileState(
                    new PileId(PileType.Stock, 0),
                    new List<PlayingCard>(Stock),
                    Stock.Count);

                var waste = new PileState(
                    new PileId(PileType.Waste, 0),
                    new List<PlayingCard>(Waste),
                    0);

                var foundations = new List<PileState>(Foundations.Length);
                for (int i = 0; i < Foundations.Length; i++)
                {
                    foundations.Add(new PileState(
                        new PileId(PileType.Foundation, i),
                        new List<PlayingCard>(Foundations[i]),
                        0));
                }

                return new TableState(stock, waste, foundations, tableaus);
            }
        }

        private class NullDealRuleForShuffle : IDealRule
        {
            private static readonly int[] KlondikeTriangleCounts = { 1, 2, 3, 4, 5, 6, 7 };

            public int TableauCount => 7;
            public int FoundationCount => 4;
            public int PerSuitCardCount => 13;
            public bool HasWaste => true;
            public bool CanRecycleStock => true;
            public int StockDrawCount => 1;
            public bool StockDealsToTableau => false;
            public int[] InitialCardCounts => KlondikeTriangleCounts;
            public int InitialFaceUpPerColumn => 1;
            public bool OnlyKingOnEmptyTableau => true;
        }
    }

    internal static class TableStateLinearizeExtensions
    {
        public static List<PlayingCard> Linearize(this TableState state)
        {
            var deck = new List<PlayingCard>(52);
            foreach (var pile in state.Tableaus)
                deck.AddRange(pile.Cards);
            deck.AddRange(state.Stock.Cards);
            foreach (var pile in state.Foundations)
                deck.AddRange(pile.Cards);
            deck.AddRange(state.Waste.Cards);
            return deck;
        }
    }
}
