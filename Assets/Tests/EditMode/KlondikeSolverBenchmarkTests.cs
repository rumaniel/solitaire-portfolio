using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Service.CardService;
using Service.GameService;

namespace Tests.EditMode
{
    /// <summary>
    /// Phase-0 benchmark for the resolve-then-share pipeline decision gate:
    /// measures solve rate and wall-clock cost of <see cref="KlondikeSolver"/>
    /// over random Fisher-Yates deals for draw-1 and draw-3 rules.
    ///
    /// Budgets escalate per seed (ladder) instead of one big budget, because an
    /// unsolvable seed burns its entire budget - a flat 200k-state budget locks
    /// the editor for 10+ minutes. The ladder caps worst-case time per seed and
    /// reports which solve rate each budget tier buys.
    /// Expensive by design - <c>[Explicit]</c>, run via Test Runner on demand.
    /// </summary>
    [TestFixture]
    public class KlondikeSolverBenchmarkTests
    {
        private const int SampleSize = 20;
        private static readonly int[] BudgetLadder = { 5_000, 20_000, 50_000 };

        [Test, Explicit("20-seed solver benchmark - expensive; opt-in only")]
        public void Benchmark_Draw1_RandomDeals() => RunBenchmark(stockDrawCount: 1, useFastSolver: false);

        [Test, Explicit("20-seed solver benchmark - expensive; opt-in only")]
        public void Benchmark_Draw3_RandomDeals() => RunBenchmark(stockDrawCount: 3, useFastSolver: false);

        [Test, Explicit("20-seed fast-solver benchmark - opt-in only")]
        public void Benchmark_Draw1_FastSolver() => RunBenchmark(stockDrawCount: 1, useFastSolver: true);

        [Test, Explicit("20-seed fast-solver benchmark - opt-in only")]
        public void Benchmark_Draw3_FastSolver() => RunBenchmark(stockDrawCount: 3, useFastSolver: true);

        // KlondikeSolver (reference) always draws to waste and cannot handle StockDealsToTableau,
        // so the Easthaven benchmark uses KlondikeFastSolver only.
        [Test, Explicit("20-seed Easthaven fast-solver benchmark - opt-in only")]
        public void Benchmark_Easthaven_FastSolver() => RunEasthavenBenchmark();

        private static void RunEasthavenBenchmark()
        {
            var rule = new StubDealRule
            {
                HasWaste = false,
                CanRecycleStock = false,
                StockDealsToTableau = true,
                InitialCardCounts = new[] { 3, 3, 3, 3, 3, 3, 3 },
                InitialFaceUpPerColumn = 3,
                OnlyKingOnEmptyTableau = false,
                StockDrawCount = 0,
            };

            int[] solvedAtTier = new int[BudgetLadder.Length];
            int unsolvable = 0;
            int exhausted = 0;
            long totalStates = 0;
            double totalMs = 0;
            var perSeedMs = new List<double>(SampleSize);

            for (int seed = 0; seed < SampleSize; seed++)
            {
                var deck = DeckFactory.CreateShuffled(seed);
                var state = DealBuilder.Build(deck, rule);

                double seedMs = 0;
                string verdict = "exhausted";
                for (int tier = 0; tier < BudgetLadder.Length; tier++)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var r = KlondikeFastSolver.Solve(state, rule, BudgetLadder[tier]);
                    sw.Stop();

                    seedMs += sw.Elapsed.TotalMilliseconds;
                    totalStates += r.StatesExplored;

                    if (r.Solved)
                    {
                        solvedAtTier[tier]++;
                        verdict = $"solved@{BudgetLadder[tier] / 1000}k";
                        break;
                    }
                    if (!r.BudgetExceeded)
                    {
                        unsolvable++;
                        verdict = "unsolvable";
                        break;
                    }
                    if (tier == BudgetLadder.Length - 1)
                        exhausted++;
                }

                perSeedMs.Add(seedMs);
                totalMs += seedMs;
                UnityEngine.Debug.Log(
                    $"[Solver benchmark fast easthaven] seed {seed}: {verdict} in {seedMs:F0}ms");
            }

            perSeedMs.Sort();
            int solvedTotal = 0;
            var tierReport = new List<string>(BudgetLadder.Length);
            for (int tier = 0; tier < BudgetLadder.Length; tier++)
            {
                solvedTotal += solvedAtTier[tier];
                tierReport.Add($"<={BudgetLadder[tier] / 1000}k: {solvedTotal}/{SampleSize}");
            }

            var report =
                $"[Solver benchmark fast easthaven] {SampleSize} seeds, ladder {string.Join("/", BudgetLadder)}\n" +
                $"  solve rate by budget - {string.Join(", ", tierReport)}\n" +
                $"  unsolvable: {unsolvable}  exhausted@{BudgetLadder[BudgetLadder.Length - 1] / 1000}k: {exhausted}\n" +
                $"  per-seed ms - median: {perSeedMs[perSeedMs.Count / 2]:F0}  max: {perSeedMs[perSeedMs.Count - 1]:F0}  total: {totalMs:F0}\n" +
                $"  states explored total: {totalStates}  (~{(totalStates > 0 ? totalMs * 1000.0 / totalStates : 0):F1}us/state)";
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log(report);
        }

        private static void RunBenchmark(int stockDrawCount, bool useFastSolver)
        {
            var rule = new StubDealRule { StockDrawCount = stockDrawCount };
            var cardService = new SolitaireCardService();
            cardService.Initialize(rule);
            string label = useFastSolver ? "fast" : "ref";

            int[] solvedAtTier = new int[BudgetLadder.Length];
            int unsolvable = 0;
            int exhausted = 0;
            long totalStates = 0;
            double totalMs = 0;
            var perSeedMs = new List<double>(SampleSize);

            for (int seed = 0; seed < SampleSize; seed++)
            {
                var deck = DeckFactory.CreateShuffled(seed);
                var state = DealBuilder.Build(deck, rule);

                double seedMs = 0;
                string verdict = "exhausted";
                for (int tier = 0; tier < BudgetLadder.Length; tier++)
                {
                    var sw = Stopwatch.StartNew();
                    bool solved, budgetExceeded;
                    int statesExplored;
                    if (useFastSolver)
                    {
                        var r = KlondikeFastSolver.Solve(state, rule, BudgetLadder[tier]);
                        (solved, statesExplored, budgetExceeded) = (r.Solved, r.StatesExplored, r.BudgetExceeded);
                    }
                    else
                    {
                        var r = KlondikeSolver.Solve(state, cardService, rule, BudgetLadder[tier]);
                        (solved, statesExplored, budgetExceeded) = (r.Solved, r.StatesExplored, r.BudgetExceeded);
                    }
                    sw.Stop();

                    seedMs += sw.Elapsed.TotalMilliseconds;
                    totalStates += statesExplored;

                    if (solved)
                    {
                        solvedAtTier[tier]++;
                        verdict = $"solved@{BudgetLadder[tier] / 1000}k";
                        break;
                    }
                    if (!budgetExceeded)
                    {
                        unsolvable++;
                        verdict = "unsolvable";
                        break;
                    }
                    if (tier == BudgetLadder.Length - 1)
                        exhausted++;
                }

                perSeedMs.Add(seedMs);
                totalMs += seedMs;
                UnityEngine.Debug.Log(
                    $"[Solver benchmark {label} draw-{stockDrawCount}] seed {seed}: {verdict} in {seedMs:F0}ms");
            }

            perSeedMs.Sort();
            int solvedTotal = 0;
            var tierReport = new List<string>(BudgetLadder.Length);
            for (int tier = 0; tier < BudgetLadder.Length; tier++)
            {
                solvedTotal += solvedAtTier[tier];
                tierReport.Add($"<={BudgetLadder[tier] / 1000}k: {solvedTotal}/{SampleSize}");
            }

            var report =
                $"[Solver benchmark {label} draw-{stockDrawCount}] {SampleSize} seeds, ladder {string.Join("/", BudgetLadder)}\n" +
                $"  solve rate by budget - {string.Join(", ", tierReport)}\n" +
                $"  unsolvable: {unsolvable}  exhausted@{BudgetLadder[BudgetLadder.Length - 1] / 1000}k: {exhausted}\n" +
                $"  per-seed ms - median: {perSeedMs[perSeedMs.Count / 2]:F0}  max: {perSeedMs[perSeedMs.Count - 1]:F0}  total: {totalMs:F0}\n" +
                $"  states explored total: {totalStates}  (~{(totalStates > 0 ? totalMs * 1000.0 / totalStates : 0):F1}us/state)";
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log(report);
        }
    }
}
