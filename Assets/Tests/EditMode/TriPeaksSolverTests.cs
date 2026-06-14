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
    /// EditMode tests for <see cref="TriPeaksSolver"/>.
    /// Covers correctness on trivially solved/winnable positions, determinism on real deals,
    /// and an [Explicit] 20-seed benchmark that mirrors the KlondikeSolverBenchmarkTests report shape.
    /// </summary>
    [TestFixture]
    public class TriPeaksSolverTests
    {
        private static readonly BoardLayout Layout = TriPeaksLayoutFactory.Create();

        // ---- helpers ----

        /// <summary>
        /// Builds a BoardState that mirrors exactly what BoardGameServiceBase.Initialize + OnDealt does:
        ///   cells[0..27] = deck[0..27], stock = deck[28..51], then OnDealt flips Stock[Count-1] to waste.
        /// </summary>
        private static BoardState DealFromSeed(int seed)
        {
            var deck = DeckFactory.CreateShuffled(seed);
            var cellCards = new List<PlayingCard>(28);
            for (int i = 0; i < 28; i++) cellCards.Add(deck[i]);
            var stock = new List<PlayingCard>(24);
            for (int i = 28; i < 52; i++) stock.Add(deck[i]);

            // Mirror OnDealt: flip Stock[Count-1] to waste.
            var initial = new BoardState(cellCards, stock, waste: null);
            return initial.WithStockDrawn();
        }

        /// <summary>
        /// Builds a BoardState where all 28 cells are empty; stock already drained; waste has one card.
        /// This is the post-win state — solver must detect it immediately.
        /// </summary>
        private static BoardState BuildClearedBoard()
        {
            // Cells all null; stock empty; waste has one card so WasteTop != null.
            var emptyCells = new PlayingCard[28]; // all null
            var waste = new List<PlayingCard> { new PlayingCard(Rank.Ace, Suit.Spade) };
            return new BoardState(emptyCells, stock: null, waste: waste);
        }

        // ---- tests ----

        [Test]
        public void Solve_ClearedBoard_IsSolvedImmediately()
        {
            var state = BuildClearedBoard();
            var result = TriPeaksSolver.Solve(state, Layout, stateBudget: 1);

            Assert.IsTrue(result.Solved, "A board with no cells should be solved immediately.");
            Assert.IsFalse(result.BudgetExceeded);
            // Visited only the initial (already-solved) state — no real search.
            Assert.AreEqual(1, result.StatesExplored);
        }

        [Test]
        public void Solve_TinyWinnablePosition_FindsSolution()
        {
            // Hand-build a single-cell board equivalent using the real 28-cell layout but
            // with only cell 18 (a base row cell, always free) occupied.
            // Waste top = 5 of Hearts; cell 18 = 4 of Spades (diff=1 → playable → win in one tap).

            var cells = new PlayingCard[28]; // all null
            cells[18] = new PlayingCard(Rank.Four, Suit.Spade);
            var waste = new List<PlayingCard> { new PlayingCard(Rank.Five, Suit.Heart) };
            var state = new BoardState(cells, stock: null, waste: waste);

            var result = TriPeaksSolver.Solve(state, Layout, stateBudget: 100);

            Assert.IsTrue(result.Solved, "Single free cell adjacent to waste-top must solve in one step.");
            Assert.IsFalse(result.BudgetExceeded);
        }

        [Test]
        public void Solve_TinyWinnablePosition_AceKingWrap_FindsSolution()
        {
            // Ace↔King wrap: diff == 12; waste top = King, cell 18 = Ace.
            var cells = new PlayingCard[28];
            cells[18] = new PlayingCard(Rank.Ace, Suit.Heart);
            var waste = new List<PlayingCard> { new PlayingCard(Rank.King, Suit.Spade) };
            var state = new BoardState(cells, stock: null, waste: waste);

            var result = TriPeaksSolver.Solve(state, Layout, stateBudget: 100);

            Assert.IsTrue(result.Solved, "Ace on King (diff=12 wrap) must be playable.");
        }

        [Test]
        public void Solve_TinyWinnablePosition_KingAceWrap_FindsSolution()
        {
            // King on Ace: reverse direction of the wrap.
            var cells = new PlayingCard[28];
            cells[18] = new PlayingCard(Rank.King, Suit.Club);
            var waste = new List<PlayingCard> { new PlayingCard(Rank.Ace, Suit.Diamond) };
            var state = new BoardState(cells, stock: null, waste: waste);

            var result = TriPeaksSolver.Solve(state, Layout, stateBudget: 100);

            Assert.IsTrue(result.Solved, "King on Ace (diff=12 wrap) must be playable.");
        }

        [Test]
        public void SteppedSolve_MatchesOneShotSolve_ForThreeSeeds()
        {
            // Verifies that sliced execution produces byte-identical results to one-shot Solve.
            // Exploration order equality is guaranteed: RunSteps resumes the same Searcher
            // instance (same DFS stack, same visited set) — the slice boundary only pauses the loop.
            int[] seeds = { 0, 7, 42 };
            const int budget = 5_000;
            const int sliceSize = 1_024;

            foreach (int seed in seeds)
            {
                var state = DealFromSeed(seed);

                var oneShot = TriPeaksSolver.Solve(state, Layout, stateBudget: budget);
                var stepped = TriPeaksSolver.SolveStepped(state, Layout, stateBudget: budget, sliceSize: sliceSize);

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
            // Two independent Solve calls on the same deal must return the same verdict.
            var state = DealFromSeed(42);

            var r1 = TriPeaksSolver.Solve(state, Layout, stateBudget: 5_000);
            var r2 = TriPeaksSolver.Solve(state, Layout, stateBudget: 5_000);

            Assert.AreEqual(r1.Solved, r2.Solved, "Solved must match across two calls.");
            Assert.AreEqual(r1.BudgetExceeded, r2.BudgetExceeded, "BudgetExceeded must match.");
            Assert.AreEqual(r1.StatesExplored, r2.StatesExplored, "StatesExplored must match (deterministic DFS).");
        }

        [Test]
        public void Solve_RandomDeal_DoesNotThrow()
        {
            // Safety net: 10 random seeds, modest budget — must complete without exception.
            for (int seed = 0; seed < 10; seed++)
            {
                var state = DealFromSeed(seed);
                Assert.DoesNotThrow(() => TriPeaksSolver.Solve(state, Layout, stateBudget: 10_000),
                    $"Solver threw for seed {seed}");
            }
        }

        [Test]
        public void Solve_EmptyWaste_NoCellPlayWithoutWasteAnchor()
        {
            // If waste is empty, no cell should be playable (mirrors service: WasteTop == null → return early).
            // Build a board with only base cells occupied and stock also empty.
            // Solver must not crash and must return not-solved (no moves at all → exhausted quickly).
            var cells = new PlayingCard[28];
            cells[18] = new PlayingCard(Rank.Five, Suit.Spade);
            // No waste, no stock — dead position with empty waste anchor.
            var state = new BoardState(cells, stock: null, waste: null);

            var result = TriPeaksSolver.Solve(state, Layout, stateBudget: 100);

            Assert.IsFalse(result.Solved);
            Assert.IsFalse(result.BudgetExceeded, "Should exhaust moves immediately, not hit budget.");
        }

        [Test]
        public void Solve_InvalidLayout_ThrowsArgumentException()
        {
            // Passing a layout with wrong cell count must throw.
            var badLayout = new BoardLayout(Model.Game.GameType.TriPeaks, 1,
                new List<BoardCell> { new BoardCell(new CellId(0), null) });
            var state = DealFromSeed(0);

            Assert.Throws<ArgumentException>(() => TriPeaksSolver.Solve(state, badLayout));
        }

        // ---- benchmark ----

        [Test, Explicit("20-seed TriPeaks solver benchmark - opt-in only")]
        public void Benchmark_RandomDeals_20Seeds()
        {
            const int SampleSize = 20;
            int[] budgetLadder = { 5_000, 20_000, 50_000 };

            int[] solvedAtTier = new int[budgetLadder.Length];
            int unsolvable = 0;
            int exhausted = 0;
            long totalStates = 0;
            double totalMs = 0;
            var perSeedMs = new List<double>(SampleSize);

            for (int seed = 0; seed < SampleSize; seed++)
            {
                var state = DealFromSeed(seed);
                double seedMs = 0;
                string verdict = "exhausted";

                for (int tier = 0; tier < budgetLadder.Length; tier++)
                {
                    var sw = Stopwatch.StartNew();
                    var r = TriPeaksSolver.Solve(state, Layout, budgetLadder[tier]);
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
                UnityEngine.Debug.Log($"[TriPeaks benchmark] seed {seed}: {verdict} in {seedMs:F0}ms");
            }

            perSeedMs.Sort();
            int solvedTotal = 0;
            var tierReport = new List<string>(budgetLadder.Length);
            for (int tier = 0; tier < budgetLadder.Length; tier++)
            {
                solvedTotal += solvedAtTier[tier];
                tierReport.Add($"<={budgetLadder[tier] / 1000}k: {solvedTotal}/{SampleSize}");
            }

            string report =
                $"[TriPeaks benchmark] {SampleSize} seeds, ladder {string.Join("/", budgetLadder)}\n" +
                $"  solve rate by budget - {string.Join(", ", tierReport)}\n" +
                $"  unsolvable: {unsolvable}  exhausted@{budgetLadder[budgetLadder.Length - 1] / 1000}k: {exhausted}\n" +
                $"  per-seed ms - median: {perSeedMs[perSeedMs.Count / 2]:F0}  max: {perSeedMs[perSeedMs.Count - 1]:F0}  total: {totalMs:F0}\n" +
                $"  states explored total: {totalStates}  (~{(totalStates > 0 ? totalMs * 1000.0 / totalStates : 0):F1}us/state)";
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log(report);
        }
    }
}
