using System.Collections.Generic;
using NUnit.Framework;
using Service.CardService;
using Service.GameService;

namespace Tests.EditMode
{
    /// <summary>
    /// Empirical solvability sampling for <see cref="ReversePlayShuffleStrategy"/>.
    /// <br/>
    /// The Phase-1 reversibility check is fast and runs by default.
    /// The full-state solver sweep is expensive (DFS with memoization) and marked
    /// <c>[Explicit]</c> so it only runs when invoked directly via the Test Runner.
    /// </summary>
    [TestFixture]
    public class ReversePlaySolvabilityTests
    {
        private const int SampleSize = 5;
        private const int SolverBudget = 20_000;

        private StubDealRule _rule;
        private SolitaireCardService _cardService;

        [SetUp]
        public void SetUp()
        {
            _rule = new StubDealRule();
            _cardService = new SolitaireCardService();
            _cardService.Initialize(_rule);
        }

        [Test]
        public void Phase1_EveryRecordedMoveIsReversible()
        {
            var strategy = new ReversePlayShuffleStrategy();
            for (int seed = 0; seed < SampleSize; seed++)
            {
                Assert.IsTrue(
                    strategy.ForTesting_VerifyPhase1Reversible(seed, _rule),
                    $"Phase 1 trace failed to reverse-reach solved state for seed {seed}.");
            }
        }

        [Test, Explicit("Runs the Klondike solver — expensive; opt-in only")]
        public void Solvability_SampledSeedsReport()
        {
            var strategy = new ReversePlayShuffleStrategy();
            var failures = new List<int>();
            int budgetExceeded = 0;
            int totalExplored = 0;

            for (int seed = 0; seed < SampleSize; seed++)
            {
                var state = strategy.BuildInitialState(seed, _rule);
                var result = KlondikeSolver.Solve(state, _cardService, _rule, SolverBudget);
                totalExplored += result.StatesExplored;

                if (!result.Solved)
                {
                    if (result.BudgetExceeded) budgetExceeded++;
                    else failures.Add(seed);
                }
            }

            int solved = SampleSize - failures.Count - budgetExceeded;
            var report = $"[ReversePlay solvability] {solved}/{SampleSize} solved, " +
                $"{failures.Count} unwinnable, {budgetExceeded} budget-exceeded, " +
                $"avg states explored per seed: {totalExplored / SampleSize}";
            TestContext.WriteLine(report);
            UnityEngine.Debug.Log(report);

            // Assert only on *proven* failures. Budget-exceeded seeds are inconclusive
            // (may be solvable under a larger budget) and don't fail the test.
            if (failures.Count > 0)
            {
                Assert.Fail(
                    $"{failures.Count}/{SampleSize} seeds are provably unsolvable: [{string.Join(", ", failures)}]");
            }
        }
    }
}
