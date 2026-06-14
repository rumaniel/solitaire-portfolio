using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Model.Board;
using Model.Card;

namespace Service.GameService
{
    /// <summary>
    /// Allocation-light depth-first Pyramid solver for runtime deal verification.
    /// Mirrors PyramidGameService's action set exactly: free-cell + waste-top matches
    /// (Kings alone or pairs summing to 13), stock draw, and waste recycle (up to
    /// maxRecycles times). Uses do/undo DFS with FNV-1a hash memoisation — the same
    /// pattern as <see cref="KlondikeFastSolver"/>.
    /// </summary>
    public static class PyramidSolver
    {
        /// <summary>Outcome of a single solve attempt.</summary>
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
        /// Solves a Pyramid deal within the given visited-state budget.
        /// Thread-safe per call (no shared mutable state outside the Searcher).
        /// </summary>
        /// <param name="initial">Starting board state (28 cell-cards + stock, empty waste, recycleCount=0).</param>
        /// <param name="layout">Board topology produced by <see cref="Service.BoardGameService.PyramidLayoutFactory"/>.</param>
        /// <param name="maxRecycles">How many times waste may be recycled (3 for the MS-style Pyramid game).</param>
        /// <param name="stateBudget">Maximum distinct states to explore before returning BudgetExceeded.</param>
        /// <param name="ct">Cancellation token; checked every 1024 iterations.</param>
        public static SolveResult Solve(
            BoardState initial,
            BoardLayout layout,
            int maxRecycles = 3,
            int stateBudget = 200_000,
            CancellationToken ct = default)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (layout.Count != PyramidCellCount)
                throw new ArgumentException(
                    $"Layout must have exactly {PyramidCellCount} cells for Pyramid; got {layout.Count}.",
                    nameof(layout));
            if (stateBudget < 1) throw new ArgumentOutOfRangeException(nameof(stateBudget), "Must be >= 1.");
            if (maxRecycles < 0) throw new ArgumentOutOfRangeException(nameof(maxRecycles), "Must be >= 0.");

            var searcher = new Searcher(initial, layout, maxRecycles);
            return searcher.Run(stateBudget, ct);
        }

        /// <summary>
        /// Async variant — time-sliced on the PlayerLoop so it is safe on WebGL where managed
        /// threads never run. Results are contract-equal to <see cref="Solve"/> for identical inputs.
        /// </summary>
        public static async UniTask<SolveResult> SolveAsync(
            BoardState initial,
            BoardLayout layout,
            int maxRecycles = 3,
            int stateBudget = 200_000,
            CancellationToken ct = default)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (layout.Count != PyramidCellCount)
                throw new ArgumentException(
                    $"Layout must have exactly {PyramidCellCount} cells for Pyramid; got {layout.Count}.",
                    nameof(layout));
            if (stateBudget < 1) throw new ArgumentOutOfRangeException(nameof(stateBudget), "Must be >= 1.");
            if (maxRecycles < 0) throw new ArgumentOutOfRangeException(nameof(maxRecycles), "Must be >= 0.");

            var searcher = new Searcher(initial, layout, maxRecycles);
            SolveResult result;
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
            BoardState initial, BoardLayout layout, int maxRecycles, int stateBudget, int sliceSize)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var searcher = new Searcher(initial, layout, maxRecycles);
            SolveResult result;
            while (!searcher.RunSteps(sliceSize, stateBudget, out result)) { }
            return result;
        }

        // ---- constants ----

        public const int PyramidCellCount = 28;
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
        private const byte EmptySlot = 0xFF; // sentinel: pyramid cell removed
        private const byte PileSeparator = 0xFE;

        // Card byte: (rank << 2) | (suit - 1).  Rank 1..13, suit 1..4 → value 4..55.
        // Always < 0xFE, so no collision with EmptySlot/PileSeparator.
        private static byte Pack(PlayingCard card) => (byte)(((int)card.Rank << 2) | ((int)card.Suit - 1));
        private static int RankOf(byte card) => card >> 2;

        // ---- move struct ----
        // All undo information is encoded in the Move itself (no separate undo stack needed).
        //
        // Field reuse by MoveKind:
        //   RemoveCell     : IdxA = cell index; CardA = saved card byte
        //   RemoveWaste    : CardA = saved waste-top card byte
        //   RemovePair     : IdxA/IdxB = cell indices; CardA/CardB = saved card bytes
        //   RemoveCellWaste: IdxA = cell index; CardA = saved cell card; CardB = saved waste-top card
        //   DrawStock      : (no extra fields — stock top and waste top are implicit from lengths)
        //   RecycleWaste   : (no extra fields — recycleCount and lengths restore from saved values)
        // OldWasteLen / OldStockLen / OldRecycle saved for every move for Draw/Recycle undo.

        private struct Move
        {
            public MoveKind Kind;
            public byte IdxA;         // cell index (A)
            public byte IdxB;         // cell index (B) for RemovePair
            public byte CardA;        // saved card byte for cell A (or waste top for RemoveWaste)
            public byte CardB;        // saved card byte for cell B (or waste top for RemoveCellWaste)
            public byte OldWasteLen;
            public byte OldStockLen;
            public byte OldRecycle;
        }

        private enum MoveKind : byte
        {
            RemoveCell,         // lone King in a free pyramid cell
            RemoveWaste,        // lone King at waste top
            RemovePair,         // two free cells summing to 13
            RemoveCellWaste,    // free cell + waste top summing to 13
            DrawStock,          // stock top → waste
            RecycleWaste,       // reverse waste → stock (recycleCount++)
        }

        private struct Frame
        {
            public int MovesStart;
            public int MoveCount;
            public int Next;
            public Move Applied;
            public bool HasApplied;
        }

        // ---- searcher ----

        private sealed class Searcher
        {
            // Precomputed cover-blocker index arrays for O(1) free-cell check
            private readonly int[][] coverBlockers;
            private readonly int maxRecycles;

            // Mutable solver state — all arrays allocated once, mutated in place via do/undo
            private readonly byte[] cells = new byte[PyramidCellCount];
            // Stock: index 0 = bottom (drawn last), stockLen-1 = top (drawn next)
            private readonly byte[] stock = new byte[52];
            // Waste: index 0 = bottom, wasteLen-1 = top (accessible)
            private readonly byte[] waste = new byte[52];
            private int stockLen;
            private int wasteLen;
            private int recycleCount;

            private readonly HashSet<ulong> visited = new();
            private Move[] arena = new Move[512];
            private int arenaTop;
            private Frame[] frames = new Frame[128];
            private int depth;
            private bool initialized;
            private int totalIterations;

            public Searcher(BoardState initial, BoardLayout layout, int maxRecycles)
            {
                this.maxRecycles = maxRecycles;

                coverBlockers = new int[PyramidCellCount][];
                foreach (var cell in layout.Cells)
                {
                    var bl = new int[cell.CoverBlockers.Count];
                    for (int b = 0; b < cell.CoverBlockers.Count; b++)
                        bl[b] = cell.CoverBlockers[b].Value;
                    coverBlockers[cell.Id.Value] = bl;
                }

                for (int i = 0; i < PyramidCellCount; i++)
                {
                    var card = initial.CardAt(new CellId(i));
                    cells[i] = card != null ? Pack(card) : EmptySlot;
                }

                stockLen = initial.Stock.Count;
                for (int i = 0; i < stockLen; i++)
                    stock[i] = Pack(initial.Stock[i]);

                wasteLen = initial.Waste.Count;
                for (int i = 0; i < wasteLen; i++)
                    waste[i] = Pack(initial.Waste[i]);

                recycleCount = initial.RecycleCount;
            }

            public SolveResult Run(int stateBudget, CancellationToken ct)
            {
                SolveResult result;
                while (!RunSteps(int.MaxValue, stateBudget, ct, out result)) { }
                return result;
            }

            /// <summary>
            /// Runs up to <paramref name="maxIterations"/> DFS iterations. Returns true when done;
            /// false when the slice budget was exhausted mid-search.
            /// </summary>
            public bool RunSteps(int maxIterations, int stateBudget, CancellationToken ct, out SolveResult result)
            {
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
                bool bounded = (maxIterations != int.MaxValue);

                while (depth > 0)
                {
                    if (bounded && sliceIterations >= maxIterations)
                    {
                        result = default;
                        return false;
                    }
                    sliceIterations++;

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

            public bool RunSteps(int maxIterations, int stateBudget, out SolveResult result)
                => RunSteps(maxIterations, stateBudget, default, out result);

            // ---- predicates ----

            private bool IsSolved()
            {
                // Win condition from BoardGameServiceBase.IsWon: !state.AnyOccupied()
                // i.e. all 28 pyramid cells must be cleared; stock/waste state is irrelevant.
                for (int i = 0; i < PyramidCellCount; i++)
                    if (cells[i] != EmptySlot) return false;
                return true;
            }

            private bool IsFree(int index)
            {
                if (cells[index] == EmptySlot) return false;
                var bl = coverBlockers[index];
                for (int b = 0; b < bl.Length; b++)
                    if (cells[bl[b]] != EmptySlot) return false;
                return true;
            }

            private bool CanRecycle() => stockLen == 0 && wasteLen > 0 && recycleCount < maxRecycles;

            // ---- move generation ----

            private void PushFrame(Move applied, bool hasApplied)
            {
                if (depth == frames.Length)
                    Array.Resize(ref frames, frames.Length * 2);

                int start = arenaTop;
                int count = GenerateMoves(start);
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

            /// <summary>
            /// Enumerates every legal move, mirroring PyramidGameService's selectable surface:
            /// free pyramid cells and the waste top. Match moves are emitted before draw/recycle
            /// to bias the search toward solving sooner, but ordering does not affect correctness.
            /// </summary>
            private int GenerateMoves(int start)
            {
                // Collect free-cell indices into stack-allocated spans to avoid per-call heap allocation.
                // stackalloc is safe here: PyramidCellCount=28 is a small, known constant.
                Span<int> freeIdx = stackalloc int[PyramidCellCount];
                Span<byte> freeCard = stackalloc byte[PyramidCellCount];
                int freeCount = 0;
                for (int i = 0; i < PyramidCellCount; i++)
                {
                    if (IsFree(i))
                    {
                        freeIdx[freeCount] = i;
                        freeCard[freeCount] = cells[i];
                        freeCount++;
                    }
                }

                bool hasWaste = wasteLen > 0;
                byte wasteTop = hasWaste ? waste[wasteLen - 1] : (byte)0;

                // Worst case: freeCount*(freeCount-1)/2 pairs + freeCount cell+waste pairs + 4 singletons + draw + recycle
                EnsureArena(start + (PyramidCellCount * PyramidCellCount / 2) + PyramidCellCount + 4);
                int n = start;

                byte ol = (byte)wasteLen;
                byte os = (byte)stockLen;
                byte oldRecycle = (byte)recycleCount;

                // --- Lone Kings (free cells) ---
                for (int i = 0; i < freeCount; i++)
                {
                    if (RankOf(freeCard[i]) == (int)Rank.King)
                    {
                        arena[n++] = new Move
                        {
                            Kind = MoveKind.RemoveCell,
                            IdxA = (byte)freeIdx[i],
                            CardA = freeCard[i],
                            OldWasteLen = ol, OldStockLen = os, OldRecycle = oldRecycle,
                        };
                    }
                }

                // --- Lone King at waste top ---
                if (hasWaste && RankOf(wasteTop) == (int)Rank.King)
                {
                    arena[n++] = new Move
                    {
                        Kind = MoveKind.RemoveWaste,
                        CardA = wasteTop,
                        OldWasteLen = ol, OldStockLen = os, OldRecycle = oldRecycle,
                    };
                }

                // --- Pairs summing to 13 among free cells ---
                for (int i = 0; i < freeCount; i++)
                {
                    int rankA = RankOf(freeCard[i]);
                    for (int j = i + 1; j < freeCount; j++)
                    {
                        if (rankA + RankOf(freeCard[j]) == 13)
                        {
                            arena[n++] = new Move
                            {
                                Kind = MoveKind.RemovePair,
                                IdxA = (byte)freeIdx[i], CardA = freeCard[i],
                                IdxB = (byte)freeIdx[j], CardB = freeCard[j],
                                OldWasteLen = ol, OldStockLen = os, OldRecycle = oldRecycle,
                            };
                        }
                    }
                }

                // --- Free cell + waste top summing to 13 ---
                if (hasWaste)
                {
                    int rankW = RankOf(wasteTop);
                    for (int i = 0; i < freeCount; i++)
                    {
                        if (RankOf(freeCard[i]) + rankW == 13)
                        {
                            arena[n++] = new Move
                            {
                                Kind = MoveKind.RemoveCellWaste,
                                IdxA = (byte)freeIdx[i], CardA = freeCard[i],
                                CardB = wasteTop,
                                OldWasteLen = ol, OldStockLen = os, OldRecycle = oldRecycle,
                            };
                        }
                    }
                }

                // --- Draw stock top → waste ---
                if (stockLen > 0)
                {
                    arena[n++] = new Move
                    {
                        Kind = MoveKind.DrawStock,
                        OldWasteLen = ol, OldStockLen = os, OldRecycle = oldRecycle,
                    };
                }

                // --- Recycle waste → stock ---
                if (CanRecycle())
                {
                    arena[n++] = new Move
                    {
                        Kind = MoveKind.RecycleWaste,
                        OldWasteLen = ol, OldStockLen = os, OldRecycle = oldRecycle,
                    };
                }

                return n - start;
            }

            private void EnsureArena(int required)
            {
                if (required <= arena.Length) return;
                int newSize = arena.Length * 2;
                while (newSize < required) newSize *= 2;
                Array.Resize(ref arena, newSize);
            }

            // ---- apply / undo ----

            private void Apply(in Move move)
            {
                switch (move.Kind)
                {
                    case MoveKind.RemoveCell:
                        cells[move.IdxA] = EmptySlot;
                        break;

                    case MoveKind.RemoveWaste:
                        wasteLen--;
                        break;

                    case MoveKind.RemovePair:
                        cells[move.IdxA] = EmptySlot;
                        cells[move.IdxB] = EmptySlot;
                        break;

                    case MoveKind.RemoveCellWaste:
                        cells[move.IdxA] = EmptySlot;
                        wasteLen--;
                        break;

                    case MoveKind.DrawStock:
                        // BoardState.WithStockDrawn: Stock[Count-1] (top) moves to Waste top
                        waste[wasteLen++] = stock[--stockLen];
                        break;

                    case MoveKind.RecycleWaste:
                        // BoardState.WithStockRecycled: waste reversed into stock, waste cleared, recycleCount++
                        // Waste[0..wasteLen-1]: index 0 = bottom, wasteLen-1 = top.
                        // List.Reverse() makes former-bottom (index 0) become the new stock top (drawn first).
                        // In our stock array the top is at stockLen-1, so:
                        //   stock[i] = waste[i]  yields waste[0] at stock[0] (bottom) ...
                        //   ...but after Reverse in the service, the order in the new stock List is:
                        //   [waste[wasteLen-1], ..., waste[0]]
                        //   (former waste-top is now stock[0] = bottom, former waste-bottom is stock-top)
                        // So our stock array should have stock[0] = waste[wasteLen-1] (former top, drawn last)
                        // and stock[stockLen-1] = waste[0] (former bottom, drawn first = new stock top).
                        stockLen = wasteLen;
                        for (int i = 0; i < stockLen; i++)
                            stock[i] = waste[wasteLen - 1 - i];
                        wasteLen = 0;
                        recycleCount++;
                        break;
                }
            }

            private void Undo(in Move move)
            {
                switch (move.Kind)
                {
                    case MoveKind.RemoveCell:
                        cells[move.IdxA] = move.CardA;
                        break;

                    case MoveKind.RemoveWaste:
                        waste[wasteLen++] = move.CardA;
                        break;

                    case MoveKind.RemovePair:
                        cells[move.IdxA] = move.CardA;
                        cells[move.IdxB] = move.CardB;
                        break;

                    case MoveKind.RemoveCellWaste:
                        cells[move.IdxA] = move.CardA;
                        waste[wasteLen++] = move.CardB; // CardB holds the saved waste-top card
                        break;

                    case MoveKind.DrawStock:
                        stock[stockLen++] = waste[--wasteLen];
                        break;

                    case MoveKind.RecycleWaste:
                        // reverse of Apply: stock → waste reversed, recycleCount--
                        // Current stock: stock[0]=former-waste-top, stock[stockLen-1]=former-waste-bottom
                        // Restore waste: waste[0]=former-bottom, waste[wasteLen-1]=former-top
                        wasteLen = stockLen;
                        for (int i = 0; i < wasteLen; i++)
                            waste[i] = stock[stockLen - 1 - i];
                        stockLen = 0;
                        recycleCount--;
                        break;
                }
            }

            // ---- state hash (FNV-1a over cell bytes + stock + waste + recycleCount) ----

            private ulong Hash()
            {
                ulong h = FnvOffsetBasis;
                for (int i = 0; i < PyramidCellCount; i++)
                    h = Fnv(h, cells[i]);
                h = Fnv(h, PileSeparator);
                for (int i = 0; i < stockLen; i++)
                    h = Fnv(h, stock[i]);
                h = Fnv(h, PileSeparator);
                for (int i = 0; i < wasteLen; i++)
                    h = Fnv(h, waste[i]);
                h = Fnv(h, PileSeparator);
                h = Fnv(h, (byte)recycleCount);
                return h;
            }

            private static ulong Fnv(ulong hash, byte value) => (hash ^ value) * FnvPrime;
        }
    }
}
