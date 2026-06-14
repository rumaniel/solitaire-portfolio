using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Model.Board;
using Model.Card;
using Service.BoardGameService;
using Service.GameService;

namespace Tests.EditMode
{
    /// <summary>
    /// Correctness and benchmark tests for <see cref="PyramidSolver"/>.
    /// The non-explicit tests run in the standard EditMode suite.
    /// </summary>
    [TestFixture]
    public class PyramidSolverTests
    {
        private BoardLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _layout = PyramidLayoutFactory.Create();
        }

        // ---- trivial already-won state ----

        [Test]
        public void Solve_EmptyPyramid_ReturnsSolvedImmediately()
        {
            // All 28 cells null (already removed), empty stock and waste.
            var emptyCells = new PlayingCard[PyramidSolver.PyramidCellCount];
            var state = new BoardState(emptyCells, stock: null, waste: null, recycleCount: 0);

            var result = PyramidSolver.Solve(state, _layout);

            Assert.IsTrue(result.Solved, "An already-cleared pyramid must be Solved.");
            Assert.AreEqual(1, result.StatesExplored, "Only the initial state should be explored.");
            Assert.IsFalse(result.BudgetExceeded);
        }

        // ---- hand-built tiny winnable state ----

        [Test]
        public void Solve_TwoFreeCardsSum13_SolvesInOneMove()
        {
            // Place exactly two cards in the bottom row (row 6, cells 21 and 22 — always free
            // because row 6 has no cover-blockers). Ace(1) + Queen(12) = 13: one removal wins.
            // All other cells are null (removed). Stock and waste empty.
            var cellCards = new PlayingCard[PyramidSolver.PyramidCellCount];
            cellCards[21] = new PlayingCard(Rank.Ace, Suit.Spade);
            cellCards[22] = new PlayingCard(Rank.Queen, Suit.Heart);

            var state = new BoardState(cellCards, stock: null, waste: null, recycleCount: 0);

            var result = PyramidSolver.Solve(state, _layout);

            Assert.IsTrue(result.Solved, "Two free cells summing to 13 must be solvable.");
            Assert.IsFalse(result.BudgetExceeded);
        }

        [Test]
        public void Solve_KingAloneInFreeCell_SolvesInOneMove()
        {
            // Single King in the bottom row (cell 27) — removed alone as a lone King.
            var cellCards = new PlayingCard[PyramidSolver.PyramidCellCount];
            cellCards[27] = new PlayingCard(Rank.King, Suit.Spade);

            var state = new BoardState(cellCards, stock: null, waste: null, recycleCount: 0);

            var result = PyramidSolver.Solve(state, _layout);

            Assert.IsTrue(result.Solved, "A lone King in a free cell must be solvable.");
            Assert.IsFalse(result.BudgetExceeded);
        }

        [Test]
        public void Solve_FreeCellPlusWasteTopSum13_SolvesInOneMove()
        {
            // One free cell Ace(1) + waste top Queen(12) = 13.
            var cellCards = new PlayingCard[PyramidSolver.PyramidCellCount];
            cellCards[21] = new PlayingCard(Rank.Ace, Suit.Spade);

            var waste = new List<PlayingCard> { new PlayingCard(Rank.Queen, Suit.Heart) };
            var state = new BoardState(cellCards, stock: null, waste: waste, recycleCount: 0);

            var result = PyramidSolver.Solve(state, _layout);

            Assert.IsTrue(result.Solved);
            Assert.IsFalse(result.BudgetExceeded);
        }

        [Test]
        public void Solve_KingAtWasteTop_SolvesInOneMove()
        {
            // Last remaining card is a King sitting on the waste top.
            var cellCards = new PlayingCard[PyramidSolver.PyramidCellCount];
            // All cells empty; King only on waste.
            var waste = new List<PlayingCard> { new PlayingCard(Rank.King, Suit.Heart) };
            var state = new BoardState(cellCards, stock: null, waste: waste, recycleCount: 0);

            // Winning condition is AnyOccupied() on cells only — waste King doesn't block win,
            // so this is *already won* before the waste-top removal is attempted.
            var result = PyramidSolver.Solve(state, _layout);

            Assert.IsTrue(result.Solved);
        }

        [Test]
        public void Solve_CardNeededFromStockViaDraw_SolvesAfterDraw()
        {
            // Cell 21 = Ace(1). Stock has one card: Queen(12).
            // Must draw to get waste top, then remove pair.
            var cellCards = new PlayingCard[PyramidSolver.PyramidCellCount];
            cellCards[21] = new PlayingCard(Rank.Ace, Suit.Spade);

            var stockList = new List<PlayingCard> { new PlayingCard(Rank.Queen, Suit.Heart) };
            var state = new BoardState(cellCards, stock: stockList, waste: null, recycleCount: 0);

            var result = PyramidSolver.Solve(state, _layout);

            Assert.IsTrue(result.Solved, "Must draw stock to expose waste top, then remove the pair.");
            Assert.IsFalse(result.BudgetExceeded);
        }

        // ---- random deal: determinism and no-throw guarantee ----

        [Test]
        public void SteppedSolve_MatchesOneShotSolve_ForThreeSeeds()
        {
            // Verifies that sliced execution produces byte-identical results to one-shot Solve.
            // Exploration order equality is guaranteed: RunSteps resumes the same Searcher
            // instance (same DFS stack, same visited set) so no state is reconsidered differently.
            int[] seeds = { 0, 7, 42 };
            const int budget = 10_000;
            const int sliceSize = 1_024;

            foreach (int seed in seeds)
            {
                var (state, layout) = BuildDeal(seed);

                var oneShot = PyramidSolver.Solve(state, layout, maxRecycles: 3, stateBudget: budget);
                var stepped = PyramidSolver.SolveStepped(state, layout, maxRecycles: 3, stateBudget: budget, sliceSize: sliceSize);

                Assert.AreEqual(oneShot.Solved, stepped.Solved,
                    $"seed {seed}: Solved mismatch");
                Assert.AreEqual(oneShot.StatesExplored, stepped.StatesExplored,
                    $"seed {seed}: StatesExplored mismatch — slicing altered exploration order");
                Assert.AreEqual(oneShot.BudgetExceeded, stepped.BudgetExceeded,
                    $"seed {seed}: BudgetExceeded mismatch");
            }
        }

        [Test]
        public void Solve_RandomDeal_IsDeterministicAcrossTwoCalls()
        {
            // Build a fresh deal the same way BoardGameServiceBase.Initialize does:
            // first 28 shuffled cards → cells, the rest → stock.
            const int seed = 42;
            var (state1, layout1) = BuildDeal(seed);
            var (state2, layout2) = BuildDeal(seed);

            var r1 = PyramidSolver.Solve(state1, layout1, maxRecycles: 3, stateBudget: 10_000);
            var r2 = PyramidSolver.Solve(state2, layout2, maxRecycles: 3, stateBudget: 10_000);

            Assert.AreEqual(r1.Solved, r2.Solved, "Same seed must yield same Solved result.");
            Assert.AreEqual(r1.StatesExplored, r2.StatesExplored, "Same seed must explore identical state count.");
            Assert.AreEqual(r1.BudgetExceeded, r2.BudgetExceeded);
        }

        [Test]
        public void Solve_RandomDeal_CompletesWithoutThrowing()
        {
            // Smoke-test several seeds at a tight budget — must not throw regardless of outcome.
            for (int seed = 0; seed < 5; seed++)
            {
                var (state, layout) = BuildDeal(seed);
                Assert.DoesNotThrow(
                    () => PyramidSolver.Solve(state, layout, maxRecycles: 3, stateBudget: 5_000),
                    $"Solver must not throw for seed {seed}");
            }
        }

        // ---- [Explicit] benchmark (copies report shape from KlondikeSolverBenchmarkTests) ----

        [Test, Explicit("20-seed Pyramid solver benchmark — expensive; opt-in only")]
        public void Benchmark_PyramidSolver_20Seeds()
        {
            const int sampleSize = 20;
            int[] budgetLadder = { 5_000, 20_000, 50_000 };

            int[] solvedAtTier = new int[budgetLadder.Length];
            int unsolvable = 0;
            int exhausted = 0;
            long totalStates = 0;
            double totalMs = 0;
            var perSeedMs = new List<double>(sampleSize);

            for (int seed = 0; seed < sampleSize; seed++)
            {
                var (state, layout) = BuildDeal(seed);

                double seedMs = 0;
                string verdict = "exhausted";

                for (int tier = 0; tier < budgetLadder.Length; tier++)
                {
                    var sw = Stopwatch.StartNew();
                    var r = PyramidSolver.Solve(state, layout, maxRecycles: 3, stateBudget: budgetLadder[tier]);
                    sw.Stop();

                    seedMs += sw.Elapsed.TotalMilliseconds;
                    totalStates += r.StatesExplored;

                    if (r.Solved)
                    {
                        solvedAtTier[tier]++;
                        verdict = $"solved@{budgetLadder[tier] / 1000}k";
                        break;
                    }
                    if (!r.BudgetExceeded)
                    {
                        unsolvable++;
                        verdict = "unsolvable";
                        break;
                    }
                    if (tier == budgetLadder.Length - 1)
                        exhausted++;
                }

                perSeedMs.Add(seedMs);
                totalMs += seedMs;
                UnityEngine.Debug.Log($"[PyramidSolver benchmark] seed {seed}: {verdict} in {seedMs:F0}ms");
            }

            perSeedMs.Sort();
            int solvedTotal = 0;
            var tierReport = new List<string>(budgetLadder.Length);
            for (int tier = 0; tier < budgetLadder.Length; tier++)
            {
                solvedTotal += solvedAtTier[tier];
                tierReport.Add($"<={budgetLadder[tier] / 1000}k: {solvedTotal}/{sampleSize}");
            }

            var report =
                $"[PyramidSolver benchmark] {sampleSize} seeds, ladder {string.Join("/", budgetLadder)}\n" +
                $"  solve rate by budget - {string.Join(", ", tierReport)}\n" +
                $"  unsolvable: {unsolvable}  exhausted@{budgetLadder[budgetLadder.Length - 1] / 1000}k: {exhausted}\n" +
                $"  per-seed ms - median: {perSeedMs[perSeedMs.Count / 2]:F0}  max: {perSeedMs[perSeedMs.Count - 1]:F0}  total: {totalMs:F0}\n" +
                $"  states explored total: {totalStates}  (~{(totalStates > 0 ? totalMs * 1000.0 / totalStates : 0):F1}us/state)";
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log(report);
        }

        // ---- helpers ----

        /// <summary>
        /// Replicates BoardGameServiceBase.Initialize deal logic exactly:
        /// shuffle deck with seed, first layout.Count cards → cells, rest → stock, waste empty.
        /// </summary>
        private static (BoardState state, BoardLayout layout) BuildDeal(int seed)
        {
            var layout = PyramidLayoutFactory.Create();
            var deck = DeckFactory.CreateShuffled(seed);

            var cellCards = new List<PlayingCard>(layout.Count);
            for (int i = 0; i < layout.Count; i++)
                cellCards.Add(deck[i]);

            var stockCards = new List<PlayingCard>(deck.Count - layout.Count);
            for (int i = layout.Count; i < deck.Count; i++)
                stockCards.Add(deck[i]);

            var state = new BoardState(cellCards, stock: stockCards, waste: null, recycleCount: 0);
            return (state, layout);
        }
    }
}
