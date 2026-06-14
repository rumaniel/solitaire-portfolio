using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Model.Board;
using Model.Card;
using Service.BoardGameService;

namespace Service.GameService
{
    /// <summary>
    /// Allocation-light depth-first TriPeaks solver for runtime winnability verification.
    /// Uses mutable do/undo state instead of cloning immutable BoardState per node.
    ///
    /// Action set mirrors TriPeaksGameService exactly:
    ///   1. Tap a free cell whose rank differs by ±1 from waste-top (Ace↔King wrap: diff 1 or 12).
    ///      The cell clears; the tapped card becomes the new waste-top.
    ///   2. Draw from stock: pop the top (last) stock card onto the waste.
    ///      Only legal when stock is non-empty.
    ///   3. No recycle: TriPeaksGameService is initialized with maxRecycles=0; an empty stock with no
    ///      playable cell is a dead end.
    ///
    /// Win: all 28 cells cleared (!AnyOccupied in service terms; cellMask == 0 here).
    /// Lose: stock empty AND no free cell matches waste-top — search backtracks.
    ///
    /// Hash: FNV-1a over cellMask (28 bits, encodes which cells remain) + stockIndex + wasteTop byte.
    ///   - cellCards are constant per deal; presence/absence is fully captured by cellMask.
    ///   - Stock order is constant per deal; draw index suffices to identify remaining stock.
    ///   - Only the current waste-top affects legality; full waste history is not needed for
    ///     state identity because you cannot "un-draw" and the waste pile can never repeat a prefix
    ///     (each new top is either a freshly-drawn stock card or a cleared cell card — both fixed per
    ///     deal — so (cellMask, stockIndex, wasteTop) uniquely identifies any reachable state).
    /// </summary>
    public static class TriPeaksSolver
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

        /// <summary>Solves a TriPeaks deal within the given visited-state budget. Thread-safe per call.</summary>
        /// <param name="initial">BoardState after OnDealt (waste already has its first card).</param>
        /// <param name="layout">The TriPeaks cover graph (28 cells, ids 0..27).</param>
        /// <param name="stateBudget">Maximum distinct states to visit before giving up.</param>
        /// <param name="ct">Cancellation token; checked every 1024 iterations.</param>
        public static SolveResult Solve(
            BoardState initial,
            BoardLayout layout,
            int stateBudget = 200_000,
            CancellationToken ct = default)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (layout.Count != TriPeaksLayoutFactory.CellCount)
                throw new ArgumentException(
                    $"Layout must have exactly {TriPeaksLayoutFactory.CellCount} cells; got {layout.Count}.",
                    nameof(layout));
            if (initial.CellCount != TriPeaksLayoutFactory.CellCount)
                throw new ArgumentException(
                    $"BoardState must have exactly {TriPeaksLayoutFactory.CellCount} cells; got {initial.CellCount}.",
                    nameof(initial));
            if (stateBudget < 1) throw new ArgumentOutOfRangeException(nameof(stateBudget), "Must be >= 1.");

            var searcher = new Searcher(initial, layout);
            return searcher.Run(stateBudget, ct);
        }

        /// <summary>
        /// Async variant — time-sliced on the PlayerLoop so it is safe on WebGL where managed
        /// threads never run. Results are contract-equal to <see cref="Solve"/> for identical inputs.
        /// </summary>
        public static async UniTask<SolveResult> SolveAsync(
            BoardState initial,
            BoardLayout layout,
            int stateBudget = 200_000,
            CancellationToken ct = default)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (layout.Count != TriPeaksLayoutFactory.CellCount)
                throw new ArgumentException(
                    $"Layout must have exactly {TriPeaksLayoutFactory.CellCount} cells; got {layout.Count}.",
                    nameof(layout));
            if (initial.CellCount != TriPeaksLayoutFactory.CellCount)
                throw new ArgumentException(
                    $"BoardState must have exactly {TriPeaksLayoutFactory.CellCount} cells; got {initial.CellCount}.",
                    nameof(initial));
            if (stateBudget < 1) throw new ArgumentOutOfRangeException(nameof(stateBudget), "Must be >= 1.");

            var searcher = new Searcher(initial, layout);
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
            BoardState initial, BoardLayout layout, int stateBudget, int sliceSize)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var searcher = new Searcher(initial, layout);
            SolveResult result;
            while (!searcher.RunSteps(sliceSize, stateBudget, out result)) { }
            return result;
        }

        // ---- cover graph precomputed as a flat array for zero-allocation IsFree checks ----

        // coverBlockers[i] is a bitmask of cells that must be absent for cell i to be free.
        private static uint[] BuildCoverBlockerMasks(BoardLayout layout)
        {
            var masks = new uint[layout.Count];
            foreach (var cell in layout.Cells)
            {
                uint mask = 0;
                foreach (var blocker in cell.CoverBlockers)
                    mask |= (1u << blocker.Value);
                masks[cell.Id.Value] = mask;
            }
            return masks;
        }

        private enum MoveKind : byte
        {
            PlayCell,   // tap a free cell onto waste
            DrawStock,  // draw top stock card to waste
        }

        private struct Move
        {
            public MoveKind Kind;
            public byte CellIndex;   // PlayCell: which cell (0..27)
            public byte PrevWaste;   // waste-top byte before this move (for undo)
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
            private const ulong FnvOffsetBasis = 14695981039346656037UL;
            private const ulong FnvPrime = 1099511628211UL;

            private const int CellCount = TriPeaksLayoutFactory.CellCount;

            // Per-deal constants (never change after construction).
            private readonly byte[] cellCard = new byte[CellCount];   // packed card per cell slot
            private readonly byte[] stockCards;                         // full stock in draw order (index 0 = bottom)
            private readonly uint[] coverBlockerMasks;                  // precomputed from layout

            // Mutable search state.
            private uint cellMask;        // bit i set = cell i still occupied
            private int stockIndex;       // next card to draw = stockCards[stockIndex]; decremented on draw
            private byte wasteTop;        // packed current waste-top card (0 = no waste)
            private bool hasWaste;        // true once at least one card is on waste (mirrors WasteTop != null)

            private readonly HashSet<ulong> visited = new();
            private Move[] arena = new Move[256];
            private int arenaTop;
            private Frame[] frames = new Frame[64];
            private int depth;
            private bool initialized;
            private int totalIterations;

            // card byte: (rank << 2) | suitIndex, rank 1..13, suitIndex 0..3
            // Using a 6-bit packing: rank occupies bits 2..6 (shift 2), suit occupies bits 0..1.
            // 0 is reserved for "no card / empty waste".
            private static byte Pack(PlayingCard card) => (byte)(((int)card.Rank << 2) | ((int)card.Suit - 1));
            private static int RankOf(byte card) => card >> 2;

            private static bool AreAdjacent(byte a, byte b)
            {
                int diff = Math.Abs(RankOf(a) - RankOf(b));
                // diff 1 = adjacent ranks; diff 12 = Ace(1)↔King(13) wrap.
                return (diff == 1) || (diff == 12);
            }

            public Searcher(BoardState initial, BoardLayout layout)
            {
                coverBlockerMasks = BuildCoverBlockerMasks(layout);

                // Pack each cell's card.
                cellMask = 0;
                for (int i = 0; i < CellCount; i++)
                {
                    var card = initial.CardAt(new CellId(i));
                    if (card != null)
                    {
                        cellCard[i] = Pack(card);
                        cellMask |= (1u << i);
                    }
                }

                // Pack stock. BoardState.Stock[last] is the next card to be drawn (service draws
                // from Stock[Count-1]). We store stock bottom-to-top so stockCards[stockIndex] is the
                // next draw, and stockIndex decrements on each draw.
                stockCards = new byte[initial.Stock.Count];
                for (int i = 0; i < initial.Stock.Count; i++)
                    stockCards[i] = Pack(initial.Stock[i]);
                // Stock[Count-1] is the next draw; we treat stockIndex as a top pointer.
                stockIndex = initial.Stock.Count - 1;

                // After OnDealt, the waste already has one card.
                if (initial.WasteTop != null)
                {
                    wasteTop = Pack(initial.WasteTop);
                    hasWaste = true;
                }
                else
                {
                    wasteTop = 0;
                    hasWaste = false;
                }
            }

            public SolveResult Run(int stateBudget, CancellationToken ct = default)
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

                    // Same 1024-stride ct check as the original monolithic loop.
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
                        if (frame.HasApplied) Undo(in frame.Applied);
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

            private bool IsSolved() => cellMask == 0;

            private bool IsFree(int cellIndex)
            {
                // Cell must be occupied and all its blockers must be absent.
                if ((cellMask & (1u << cellIndex)) == 0) return false;
                return (cellMask & coverBlockerMasks[cellIndex]) == 0;
            }

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

            private void EnsureArena(int required)
            {
                if (required <= arena.Length) return;
                int size = arena.Length * 2;
                while (size < required) size *= 2;
                Array.Resize(ref arena, size);
            }

            private int GenerateMoves(int start)
            {
                // Worst case: 28 PlayCell + 1 DrawStock.
                EnsureArena(start + CellCount + 1);
                int n = start;

                // PlayCell moves: enumerate all free cells that match waste-top.
                // Mirror service: if WasteTop == null no cell can play.
                if (hasWaste)
                {
                    for (int i = 0; i < CellCount; i++)
                    {
                        if (!IsFree(i)) continue;
                        if (!AreAdjacent(cellCard[i], wasteTop)) continue;
                        arena[n++] = new Move { Kind = MoveKind.PlayCell, CellIndex = (byte)i, PrevWaste = wasteTop };
                    }
                }

                // DrawStock: legal only when stock is non-empty.
                // Mirror service HasAnyMove: stock remaining → draw is always available.
                if (stockIndex >= 0)
                    arena[n++] = new Move { Kind = MoveKind.DrawStock, PrevWaste = wasteTop };

                return n - start;
            }

            // ---- apply / undo ----

            private void Apply(in Move move)
            {
                switch (move.Kind)
                {
                    case MoveKind.PlayCell:
                        cellMask &= ~(1u << move.CellIndex);
                        wasteTop = cellCard[move.CellIndex];
                        hasWaste = true;
                        break;

                    case MoveKind.DrawStock:
                        wasteTop = stockCards[stockIndex--];
                        hasWaste = true;
                        break;
                }
            }

            private void Undo(in Move move)
            {
                switch (move.Kind)
                {
                    case MoveKind.PlayCell:
                        cellMask |= (1u << move.CellIndex);
                        wasteTop = move.PrevWaste;
                        // hasWaste: if PrevWaste is 0 we had no waste before. Pack never produces 0
                        // for a real card (rank >= 1, so Pack >= 4), so 0 == no waste is unambiguous.
                        hasWaste = (move.PrevWaste != 0);
                        break;

                    case MoveKind.DrawStock:
                        stockIndex++;
                        wasteTop = move.PrevWaste;
                        hasWaste = (move.PrevWaste != 0);
                        break;
                }
            }

            // ---- state hash (FNV-1a) ----

            private ulong Hash()
            {
                // cellMask (4 bytes) uniquely identifies which tableau positions remain, given
                // constant card placement per deal.  stockIndex (1 byte as relative value) encodes
                // remaining stock given constant order.  wasteTop (1 byte) is the sole legality
                // anchor for cell plays.  Together these three values are a complete state identity.
                ulong h = FnvOffsetBasis;
                h = Fnv(h, (byte)(cellMask & 0xFF));
                h = Fnv(h, (byte)((cellMask >> 8) & 0xFF));
                h = Fnv(h, (byte)((cellMask >> 16) & 0xFF));
                h = Fnv(h, (byte)((cellMask >> 24) & 0xFF));
                // stockIndex can be -1 (stock exhausted); clamp to byte by adding 1 (range 0..25).
                h = Fnv(h, (byte)(stockIndex + 1));
                h = Fnv(h, wasteTop);
                return h;
            }

            private static ulong Fnv(ulong hash, byte value) => (hash ^ value) * FnvPrime;
        }
    }
}
