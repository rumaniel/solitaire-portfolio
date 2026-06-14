using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Model.Card;
using Model.Game;

namespace Service.GameService
{
    /// <summary>Shared slice size for all resumable solvers — ~2 048 states keeps each
    /// PlayerLoop slice well under 20 ms on low-end hardware.</summary>
    internal static class SolverSlicing
    {
        internal const int StatesPerSlice = 2_048;
    }

    /// <summary>
    /// Allocation-light depth-first solver for runtime deal verification.
    /// Searches a mutable byte-packed state with do/undo moves instead of cloning
    /// immutable TableState per node (the per-state cost that made
    /// <see cref="KlondikeSolver"/> editor/test-only). Move legality and exploration
    /// priorities mirror MoveEnumerator + SolitaireCardService.
    /// Foundations are tracked as per-suit counts, which also collapses the
    /// empty-foundation symmetry the pile-indexed model explores redundantly.
    ///
    /// Supported rule shapes:
    ///   Klondike — HasWaste=true, StockDealsToTableau=false (draw N cards to waste)
    ///   Easthaven — HasWaste=false, StockDealsToTableau=true (deal one card to each tableau)
    /// </summary>
    public static class KlondikeFastSolver
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

        /// <summary>
        /// Solves a deal within the given visited-state budget. Thread-safe per call.
        /// Accepts Klondike (HasWaste=true, StockDealsToTableau=false) or
        /// Easthaven (HasWaste=false, StockDealsToTableau=true) rule shapes.
        /// </summary>
        public static SolveResult Solve(TableState initial, IDealRule rule, int stateBudget = 200_000, CancellationToken ct = default)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (rule.FoundationCount != 4 || rule.PerSuitCardCount != 13)
                throw new ArgumentException(
                    "KlondikeFastSolver only supports standard 4-suit x 13-rank rules.",
                    nameof(rule));
            // Exactly one of the two supported rule shapes must match.
            bool isKlondike = rule.HasWaste && !rule.StockDealsToTableau;
            bool isEasthaven = !rule.HasWaste && rule.StockDealsToTableau;
            if (!isKlondike && !isEasthaven)
                throw new ArgumentException(
                    "KlondikeFastSolver requires either Klondike shape (HasWaste=true, StockDealsToTableau=false) " +
                    "or Easthaven shape (HasWaste=false, StockDealsToTableau=true).",
                    nameof(rule));

            var searcher = new Searcher(initial, rule);
            return searcher.Run(stateBudget, ct);
        }

        /// <summary>
        /// Async variant — time-sliced on the PlayerLoop so it is safe on WebGL where managed
        /// threads never run. Each slice processes at most <see cref="SolverSlicing.StatesPerSlice"/>
        /// DFS iterations before yielding back to the PlayerLoop. Results are contract-equal to
        /// <see cref="Solve"/> for identical inputs (same exploration order, same budget semantics).
        /// </summary>
        public static async UniTask<SolveResult> SolveAsync(
            TableState initial, IDealRule rule, int stateBudget = 200_000, CancellationToken ct = default)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (rule.FoundationCount != 4 || rule.PerSuitCardCount != 13)
                throw new ArgumentException(
                    "KlondikeFastSolver only supports standard 4-suit x 13-rank rules.", nameof(rule));
            bool isKlondike = rule.HasWaste && !rule.StockDealsToTableau;
            bool isEasthaven = !rule.HasWaste && rule.StockDealsToTableau;
            if (!isKlondike && !isEasthaven)
                throw new ArgumentException(
                    "KlondikeFastSolver requires either Klondike or Easthaven shape.", nameof(rule));

            var searcher = new Searcher(initial, rule);
            SolveResult result;
            // ct.ThrowIfCancellationRequested is also called by UniTask.Yield, so between
            // every slice we get a cancellation check even without a 1024-stride internal one.
            while (!searcher.RunSteps(SolverSlicing.StatesPerSlice, stateBudget, out result))
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            return result;
        }

        /// <summary>
        /// Synchronous stepped solve used by tests to verify that sliced execution produces
        /// byte-identical results to the one-shot <see cref="Solve"/> path.
        /// </summary>
        internal static SolveResult SolveStepped(
            TableState initial, IDealRule rule, int stateBudget, int sliceSize)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            var searcher = new Searcher(initial, rule);
            SolveResult result;
            while (!searcher.RunSteps(sliceSize, stateBudget, out result)) { }
            return result;
        }

        private enum MoveKind : byte
        {
            StockDraw,
            WasteToFoundation,
            WasteToTableau,
            TableauToFoundation,
            TableauToTableau,
            FoundationToTableau,
            StockDealToTableaus,
        }

        private struct Move
        {
            public MoveKind Kind;
            public byte Src;       // tableau source column, or suit index for FoundationToTableau
            public byte SrcIndex;  // tableau source row (TableauToTableau)
            public byte Dst;       // target column
            public byte Data;      // card byte (waste/tableau to foundation), run length (T2T), drawn count (StockDraw, 0 = recycle), deal count (StockDealToTableaus)
            public byte OldFaceUp; // source faceUpFrom before the move, for undo
            public short Priority;
        }

        private struct Frame
        {
            public int MovesStart;
            public int MoveCount;
            public int Next;
            public Move Applied;
            public bool HasApplied;
        }

        private sealed class Searcher
        {
            private const int MaxColumnCapacity = 52;
            private const ulong FnvOffsetBasis = 14695981039346656037UL;
            private const ulong FnvPrime = 1099511628211UL;
            private const byte PileSeparator = 0xFF;

            private readonly int tableauCount;
            private readonly int drawCount;
            private readonly bool canRecycle;
            private readonly bool onlyKingOnEmpty;
            private readonly int perSuitCardCount;
            private readonly bool stockDealsToTableau;
            // Worst-case moves per state: tableau-to-tableau (n*(n-1)*13) + waste-to-foundation (1)
            // + waste-to-tableau (n) + tableau-to-foundation (n) + foundation-to-tableau (4*n) + stock-draw/deal (1).
            private readonly int maxMovesPerState;

            private readonly byte[][] tableau;
            private readonly int[] tableauTop;
            private readonly int[] faceUpFrom;
            private readonly byte[] stock = new byte[MaxColumnCapacity];
            private readonly byte[] waste = new byte[MaxColumnCapacity];
            private int stockCount;
            private int wasteCount;
            private readonly int[] foundation = new int[4]; // count per suit index (Suit - 1)

            private readonly HashSet<ulong> visited = new();
            private Move[] arena = new Move[1024];
            private int arenaTop;
            private Frame[] frames = new Frame[64];
            private int depth;
            private bool initialized;
            private int totalIterations;

            public Searcher(TableState initial, IDealRule rule)
            {
                tableauCount = initial.Tableaus.Count;
                drawCount = rule.StockDrawCount;
                canRecycle = rule.CanRecycleStock;
                onlyKingOnEmpty = rule.OnlyKingOnEmptyTableau;
                perSuitCardCount = rule.PerSuitCardCount;
                stockDealsToTableau = rule.StockDealsToTableau;
                maxMovesPerState = (tableauCount * (tableauCount - 1) * 13)
                    + 1 + tableauCount + tableauCount + (4 * tableauCount) + 1;

                tableau = new byte[tableauCount][];
                tableauTop = new int[tableauCount];
                faceUpFrom = new int[tableauCount];
                for (int c = 0; c < tableauCount; c++)
                {
                    tableau[c] = new byte[MaxColumnCapacity];
                    var pile = initial.Tableaus[c];
                    tableauTop[c] = pile.Cards.Count;
                    faceUpFrom[c] = pile.FaceUpFromIndex;
                    for (int i = 0; i < pile.Cards.Count; i++)
                        tableau[c][i] = Pack(pile.Cards[i]);
                }

                stockCount = initial.Stock.Cards.Count;
                for (int i = 0; i < stockCount; i++)
                    stock[i] = Pack(initial.Stock.Cards[i]);

                wasteCount = initial.Waste.Cards.Count;
                for (int i = 0; i < wasteCount; i++)
                    waste[i] = Pack(initial.Waste.Cards[i]);

                foreach (var pile in initial.Foundations)
                {
                    if (pile.Cards.Count == 0) continue;
                    foundation[SuitIndex(Pack(pile.Cards[0]))] = pile.Cards.Count;
                }
            }

            public SolveResult Run(int stateBudget, CancellationToken ct = default)
            {
                SolveResult result;
                // Single call with int.MaxValue iterations — never pauses between slices.
                // ct is forwarded so the 1024-stride check fires on the sync path.
                while (!RunSteps(int.MaxValue, stateBudget, ct, out result)) { }
                return result;
            }

            /// <summary>
            /// Runs up to <paramref name="maxIterations"/> DFS iterations. Returns true when the
            /// search has finished (result is valid); false when the slice budget was exhausted
            /// mid-search (caller should re-enter). All Searcher fields persist between calls so
            /// exploration order and budget semantics are identical to the single-call path.
            /// The <paramref name="ct"/> is checked every 1024 total iterations (same stride as
            /// the previous monolithic loop), preserving cancellation responsiveness on the sync
            /// thread-pool path.
            /// </summary>
            public bool RunSteps(int maxIterations, int stateBudget, CancellationToken ct, out SolveResult result)
            {
                // One-time init: hash root, check immediate win, push first frame.
                if (!initialized)
                {
                    initialized = true;
                    visited.Add(Hash());
                    if (IsSolved())
                    {
                        result = new SolveResult(true, visited.Count, false);
                        return true;
                    }
                    PushFrame(default, hasApplied: false);
                }

                int sliceIterations = 0;
                // When maxIterations == int.MaxValue the slice check is disabled (sync path).
                bool bounded = (maxIterations != int.MaxValue);

                while (depth > 0)
                {
                    if (bounded && sliceIterations >= maxIterations)
                    {
                        result = default;
                        return false;
                    }
                    sliceIterations++;

                    // 1024-stride ct check — preserves the original responsiveness contract.
                    if ((++totalIterations & 0x3FF) == 0)
                        ct.ThrowIfCancellationRequested();

                    ref Frame frame = ref frames[depth - 1];
                    if (frame.Next < frame.MoveCount)
                    {
                        Move move = arena[frame.MovesStart + frame.Next];
                        frame.Next++;

                        Apply(in move);
                        if (!visited.Add(Hash()))
                        {
                            Undo(in move);
                            continue;
                        }
                        if (visited.Count > stateBudget)
                        {
                            result = new SolveResult(false, visited.Count, true);
                            return true;
                        }
                        if (IsSolved())
                        {
                            result = new SolveResult(true, visited.Count, false);
                            return true;
                        }

                        PushFrame(move, hasApplied: true);
                    }
                    else
                    {
                        if (frame.HasApplied)
                            Undo(in frame.Applied);
                        arenaTop = frame.MovesStart;
                        depth--;
                    }
                }

                result = new SolveResult(false, visited.Count, false);
                return true;
            }

            // Overload without ct for the async path (ct checked by caller between slices).
            public bool RunSteps(int maxIterations, int stateBudget, out SolveResult result)
                => RunSteps(maxIterations, stateBudget, default, out result);

            // ---- card byte: (rank << 3) | suit, suit 1..4, rank 1..13 ----

            private static byte Pack(PlayingCard card) => (byte)(((int)card.Rank << 3) | (int)card.Suit);
            private static int RankOf(byte card) => card >> 3;
            private static int SuitIndex(byte card) => (card & 0x07) - 1;
            private static bool IsRed(byte card)
            {
                int suit = card & 0x07;
                return suit == (int)Suit.Heart || suit == (int)Suit.Diamond;
            }
            private static bool IsOppositeColor(byte a, byte b) => IsRed(a) != IsRed(b);
            private static bool FitsOnTableauTop(byte moving, byte top)
                => IsOppositeColor(moving, top) && (RankOf(moving) == RankOf(top) - 1);

            private bool FitsFoundation(byte card) => RankOf(card) == foundation[SuitIndex(card)] + 1;

            private bool IsSolved()
            {
                for (int s = 0; s < 4; s++)
                    if (foundation[s] != perSuitCardCount) return false;
                return true;
            }

            // ---- move generation (mirrors MoveEnumerator order and priorities) ----

            private void PushFrame(Move applied, bool hasApplied)
            {
                if (depth == frames.Length)
                    Array.Resize(ref frames, frames.Length * 2);

                int start = arenaTop;
                int count = GenerateMoves(start);
                SortByPriorityDescending(start, count);

                frames[depth] = new Frame
                {
                    MovesStart = start,
                    MoveCount = count,
                    Next = 0,
                    Applied = applied,
                    HasApplied = hasApplied,
                };
                arenaTop = start + count;
                depth++;
            }

            private int GenerateMoves(int start)
            {
                EnsureArena(start + maxMovesPerState);
                int n = start;

                if (wasteCount > 0)
                {
                    byte card = waste[wasteCount - 1];
                    if (FitsFoundation(card))
                        arena[n++] = new Move { Kind = MoveKind.WasteToFoundation, Data = card, Priority = 90 };
                    for (int t = 0; t < tableauCount; t++)
                    {
                        if (FitsTableau(card, t))
                            arena[n++] = new Move { Kind = MoveKind.WasteToTableau, Dst = (byte)t, Priority = 50 };
                    }
                }

                for (int s = 0; s < tableauCount; s++)
                {
                    if (tableauTop[s] == 0) continue;
                    byte card = tableau[s][tableauTop[s] - 1];
                    if (!FitsFoundation(card)) continue;
                    short priority = (short)(Reveals(s, tableauTop[s] - 1) ? 120 : 100);
                    arena[n++] = new Move
                    {
                        Kind = MoveKind.TableauToFoundation,
                        Src = (byte)s,
                        Data = card,
                        OldFaceUp = (byte)faceUpFrom[s],
                        Priority = priority,
                    };
                }

                for (int s = 0; s < tableauCount; s++)
                {
                    // faceUpFrom == count means no face-up card; MoveEnumerator yields nothing there.
                    if (tableauTop[s] == 0 || faceUpFrom[s] >= tableauTop[s]) continue;
                    int runStart = ComputeRunStart(s);
                    for (int i = runStart; i < tableauTop[s]; i++)
                    {
                        byte bottom = tableau[s][i];
                        for (int t = 0; t < tableauCount; t++)
                        {
                            if (t == s) continue;
                            if (tableauTop[t] == 0)
                            {
                                if (onlyKingOnEmpty && RankOf(bottom) != (int)Rank.King) continue;
                                if (IsUselessKingMove(s, i, bottom)) continue;
                            }
                            else if (!FitsOnTableauTop(bottom, tableau[t][tableauTop[t] - 1]))
                            {
                                continue;
                            }

                            arena[n++] = new Move
                            {
                                Kind = MoveKind.TableauToTableau,
                                Src = (byte)s,
                                SrcIndex = (byte)i,
                                Dst = (byte)t,
                                Data = (byte)(tableauTop[s] - i),
                                OldFaceUp = (byte)faceUpFrom[s],
                                Priority = (short)(Reveals(s, i) ? 80 : 20),
                            };
                        }
                    }
                }

                for (int suit = 0; suit < 4; suit++)
                {
                    if (foundation[suit] == 0) continue;
                    byte card = (byte)((foundation[suit] << 3) | (suit + 1));
                    for (int t = 0; t < tableauCount; t++)
                    {
                        if (FitsTableau(card, t))
                        {
                            arena[n++] = new Move
                            {
                                Kind = MoveKind.FoundationToTableau,
                                Src = (byte)suit,
                                Dst = (byte)t,
                                Priority = 5,
                            };
                        }
                    }
                }

                if (stockDealsToTableau)
                {
                    // Easthaven: deal one card face-up to each tableau column.
                    if (stockCount > 0)
                    {
                        arena[n++] = new Move
                        {
                            Kind = MoveKind.StockDealToTableaus,
                            Data = (byte)Math.Min(tableauCount, stockCount),
                            Priority = 10,
                        };
                    }
                }
                else
                {
                    // Klondike: draw to waste or recycle waste back to stock.
                    if (stockCount > 0)
                    {
                        arena[n++] = new Move
                        {
                            Kind = MoveKind.StockDraw,
                            Data = (byte)Math.Min(drawCount, stockCount),
                            Priority = 10,
                        };
                    }
                    else if (canRecycle && wasteCount > 0)
                    {
                        arena[n++] = new Move { Kind = MoveKind.StockDraw, Data = 0, Priority = 10 };
                    }
                }

                return n - start;
            }

            private bool FitsTableau(byte card, int target)
            {
                if (tableauTop[target] == 0)
                    return !onlyKingOnEmpty || RankOf(card) == (int)Rank.King;
                return FitsOnTableauTop(card, tableau[target][tableauTop[target] - 1]);
            }

            private bool Reveals(int column, int index) => index > 0 && (index - 1) < faceUpFrom[column];

            /// <summary>Mirrors MoveEnumerator.IsUselessKingMove: a King run occupying the whole pile moved to an empty column is a no-op.</summary>
            private bool IsUselessKingMove(int source, int index, byte bottom)
                => RankOf(bottom) == (int)Rank.King && index == faceUpFrom[source] && index == 0;

            /// <summary>
            /// Deepest face-up index from which the suffix forms an alternating-color
            /// descending run - the only suffixes SolitaireCardService accepts for multi-card moves.
            /// </summary>
            private int ComputeRunStart(int column)
            {
                var pile = tableau[column];
                int idx = tableauTop[column] - 1;
                while (idx - 1 >= faceUpFrom[column]
                       && IsOppositeColor(pile[idx - 1], pile[idx])
                       && RankOf(pile[idx]) == RankOf(pile[idx - 1]) - 1)
                {
                    idx--;
                }
                return idx;
            }

            private void EnsureArena(int required)
            {
                if (required <= arena.Length) return;
                int newSize = arena.Length * 2;
                while (newSize < required) newSize *= 2;
                Array.Resize(ref arena, newSize);
            }

            private void SortByPriorityDescending(int start, int count)
            {
                for (int i = start + 1; i < start + count; i++)
                {
                    Move key = arena[i];
                    int j = i - 1;
                    while (j >= start && arena[j].Priority < key.Priority)
                    {
                        arena[j + 1] = arena[j];
                        j--;
                    }
                    arena[j + 1] = key;
                }
            }

            // ---- apply / undo ----

            private void Apply(in Move move)
            {
                switch (move.Kind)
                {
                    case MoveKind.StockDraw:
                        if (move.Data > 0)
                        {
                            for (int k = 0; k < move.Data; k++)
                                waste[wasteCount++] = stock[--stockCount];
                        }
                        else
                        {
                            for (int i = wasteCount - 1; i >= 0; i--)
                                stock[stockCount++] = waste[i];
                            wasteCount = 0;
                        }
                        break;

                    case MoveKind.WasteToFoundation:
                        wasteCount--;
                        foundation[SuitIndex(move.Data)]++;
                        break;

                    case MoveKind.WasteToTableau:
                        tableau[move.Dst][tableauTop[move.Dst]++] = waste[--wasteCount];
                        break;

                    case MoveKind.TableauToFoundation:
                        tableauTop[move.Src]--;
                        foundation[SuitIndex(move.Data)]++;
                        FlipAfterRemoval(move.Src);
                        break;

                    case MoveKind.TableauToTableau:
                    {
                        int run = move.Data;
                        var src = tableau[move.Src];
                        var dst = tableau[move.Dst];
                        for (int k = 0; k < run; k++)
                            dst[tableauTop[move.Dst] + k] = src[move.SrcIndex + k];
                        tableauTop[move.Dst] += run;
                        tableauTop[move.Src] = move.SrcIndex;
                        FlipAfterRemoval(move.Src);
                        break;
                    }

                    case MoveKind.FoundationToTableau:
                    {
                        byte card = (byte)((foundation[move.Src] << 3) | (move.Src + 1));
                        foundation[move.Src]--;
                        tableau[move.Dst][tableauTop[move.Dst]++] = card;
                        break;
                    }

                    case MoveKind.StockDealToTableaus:
                    {
                        // Mirror DealStockToTableaus: draw from stock top, deal to columns 0..Data-1 face-up.
                        // faceUpFrom of receiving columns is unchanged (they are already all face-up).
                        int count = move.Data;
                        for (int i = 0; i < count; i++)
                            tableau[i][tableauTop[i]++] = stock[--stockCount];
                        break;
                    }
                }
            }

            private void Undo(in Move move)
            {
                switch (move.Kind)
                {
                    case MoveKind.StockDraw:
                        if (move.Data > 0)
                        {
                            for (int k = 0; k < move.Data; k++)
                                stock[stockCount++] = waste[--wasteCount];
                        }
                        else
                        {
                            for (int i = stockCount - 1; i >= 0; i--)
                                waste[wasteCount++] = stock[i];
                            stockCount = 0;
                        }
                        break;

                    case MoveKind.WasteToFoundation:
                        foundation[SuitIndex(move.Data)]--;
                        waste[wasteCount++] = move.Data;
                        break;

                    case MoveKind.WasteToTableau:
                        waste[wasteCount++] = tableau[move.Dst][--tableauTop[move.Dst]];
                        break;

                    case MoveKind.TableauToFoundation:
                        foundation[SuitIndex(move.Data)]--;
                        tableau[move.Src][tableauTop[move.Src]++] = move.Data;
                        faceUpFrom[move.Src] = move.OldFaceUp;
                        break;

                    case MoveKind.TableauToTableau:
                    {
                        int run = move.Data;
                        var src = tableau[move.Src];
                        var dst = tableau[move.Dst];
                        tableauTop[move.Dst] -= run;
                        for (int k = 0; k < run; k++)
                            src[move.SrcIndex + k] = dst[tableauTop[move.Dst] + k];
                        tableauTop[move.Src] = move.SrcIndex + run;
                        faceUpFrom[move.Src] = move.OldFaceUp;
                        break;
                    }

                    case MoveKind.FoundationToTableau:
                        tableauTop[move.Dst]--;
                        foundation[move.Src]++;
                        break;

                    case MoveKind.StockDealToTableaus:
                    {
                        int count = move.Data;
                        for (int i = count - 1; i >= 0; i--)
                            stock[stockCount++] = tableau[i][--tableauTop[i]];
                        break;
                    }
                }
            }

            /// <summary>Mirrors KlondikeSolver.RemoveCardsFrom: exposing a face-down top flips it face-up.</summary>
            private void FlipAfterRemoval(int column)
            {
                int count = tableauTop[column];
                if (count == 0)
                    faceUpFrom[column] = 0;
                else if (faceUpFrom[column] >= count)
                    faceUpFrom[column] = count - 1;
            }

            // ---- state hash ----

            private ulong Hash()
            {
                ulong h = FnvOffsetBasis;
                for (int c = 0; c < tableauCount; c++)
                {
                    h = Fnv(h, (byte)faceUpFrom[c]);
                    var pile = tableau[c];
                    int top = tableauTop[c];
                    for (int i = 0; i < top; i++)
                        h = Fnv(h, pile[i]);
                    h = Fnv(h, PileSeparator);
                }
                for (int i = 0; i < stockCount; i++)
                    h = Fnv(h, stock[i]);
                h = Fnv(h, PileSeparator);
                for (int i = 0; i < wasteCount; i++)
                    h = Fnv(h, waste[i]);
                h = Fnv(h, PileSeparator);
                for (int s = 0; s < 4; s++)
                    h = Fnv(h, (byte)foundation[s]);
                return h;
            }

            private static ulong Fnv(ulong hash, byte value) => (hash ^ value) * FnvPrime;
        }
    }
}
