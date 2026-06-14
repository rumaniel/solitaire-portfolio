using System.Collections.Generic;
using NUnit.Framework;
using Model.Card;
using Model.Game;
using Service.CardService;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class KlondikeFastSolverTests
    {
        private StubDealRule _rule;

        [SetUp]
        public void SetUp()
        {
            _rule = new StubDealRule();
        }

        [Test]
        public void Solve_AlreadySolvedState_ReturnsSolvedImmediately()
        {
            var state = BuildFullySolved(_rule);
            var result = KlondikeFastSolver.Solve(state, _rule, stateBudget: 100);

            Assert.IsTrue(result.Solved);
            Assert.IsFalse(result.BudgetExceeded);
        }

        [Test]
        public void Solve_SolvedShuffleStrategyBaseline_IsWinnable()
        {
            var deck = new SolvedShuffleStrategy(extraShuffles: 0).Shuffle(seed: 42);
            var state = DealBuilder.Build(deck, _rule);
            var result = KlondikeFastSolver.Solve(state, _rule, stateBudget: 10_000);

            Assert.IsTrue(result.Solved,
                $"SolvedShuffleStrategy baseline must be solvable. Explored {result.StatesExplored} states, budgetExceeded={result.BudgetExceeded}");
        }

        [Test]
        public void Solve_SameDeal_IsDeterministic()
        {
            var deck = DeckFactory.CreateShuffled(seed: 7);
            var state = DealBuilder.Build(deck, _rule);

            var first = KlondikeFastSolver.Solve(state, _rule, stateBudget: 5_000);
            var second = KlondikeFastSolver.Solve(state, _rule, stateBudget: 5_000);

            Assert.AreEqual(first.Solved, second.Solved);
            Assert.AreEqual(first.StatesExplored, second.StatesExplored);
            Assert.AreEqual(first.BudgetExceeded, second.BudgetExceeded);
        }

        [Test]
        public void Solve_Draw3Rule_RunsWithoutError()
        {
            var rule = new StubDealRule { StockDrawCount = 3 };
            var deck = DeckFactory.CreateShuffled(seed: 7);
            var state = DealBuilder.Build(deck, rule);

            var result = KlondikeFastSolver.Solve(state, rule, stateBudget: 5_000);

            Assert.IsTrue(result.StatesExplored > 0);
        }

        [Test]
        public void Solve_Easthaven_DoesNotThrowAndExploresStates()
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
            var deck = DeckFactory.CreateShuffled(seed: 7);
            var state = DealBuilder.Build(deck, rule);

            var result = KlondikeFastSolver.Solve(state, rule, stateBudget: 5_000);

            Assert.IsTrue(result.StatesExplored > 0,
                $"Easthaven solver must explore at least one state; BudgetExceeded={result.BudgetExceeded}");
        }

        [Test]
        public void Solve_Easthaven_IsDeterministic()
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
            var deck = DeckFactory.CreateShuffled(seed: 7);
            var state = DealBuilder.Build(deck, rule);

            var first = KlondikeFastSolver.Solve(state, rule, stateBudget: 5_000);
            var second = KlondikeFastSolver.Solve(state, rule, stateBudget: 5_000);

            Assert.AreEqual(first.Solved, second.Solved);
            Assert.AreEqual(first.StatesExplored, second.StatesExplored);
            Assert.AreEqual(first.BudgetExceeded, second.BudgetExceeded);
        }

        [Test]
        public void SteppedSolve_MatchesOneShotSolve_ForThreeSeeds()
        {
            // Verifies that sliced execution (SolveStepped) produces byte-identical results
            // to the one-shot Solve path for any slice size. Exploration order equality is
            // guaranteed because RunSteps resumes the same Searcher instance without re-sorting
            // or re-hashing any state — it literally continues the same DFS stack.
            int[] seeds = { 1, 42, 999 };
            const int budget = 5_000;
            const int sliceSize = 1_024;

            foreach (int seed in seeds)
            {
                var deck = DeckFactory.CreateShuffled(seed);
                var state = DealBuilder.Build(deck, _rule);

                var oneShot = KlondikeFastSolver.Solve(state, _rule, budget);
                var stepped = KlondikeFastSolver.SolveStepped(state, _rule, budget, sliceSize);

                Assert.AreEqual(oneShot.Solved, stepped.Solved,
                    $"seed {seed}: Solved mismatch");
                Assert.AreEqual(oneShot.StatesExplored, stepped.StatesExplored,
                    $"seed {seed}: StatesExplored mismatch — slicing altered exploration order");
                Assert.AreEqual(oneShot.BudgetExceeded, stepped.BudgetExceeded,
                    $"seed {seed}: BudgetExceeded mismatch");
            }
        }

        [Test, Explicit("Runs the slow reference solver for parity - expensive; opt-in only")]
        public void Parity_SeedsSolvedByReferenceSolver_AreSolvedByFastSolver()
        {
            var cardService = new SolitaireCardService();
            cardService.Initialize(_rule);

            var mismatches = new List<int>();
            for (int seed = 0; seed < 20; seed++)
            {
                var state = DealBuilder.Build(DeckFactory.CreateShuffled(seed), _rule);
                var reference = KlondikeSolver.Solve(state, cardService, _rule, stateBudget: 5_000);
                if (!reference.Solved) continue;

                // 4x budget headroom: exploration order differs, but anything the
                // reference proves solvable must also be provable by the fast solver.
                var fast = KlondikeFastSolver.Solve(state, _rule, stateBudget: 20_000);
                if (!fast.Solved) mismatches.Add(seed);
            }

            Assert.IsEmpty(mismatches,
                $"Fast solver failed on reference-solved seeds: [{string.Join(", ", mismatches)}]");
        }

        private static TableState BuildFullySolved(IDealRule rule)
        {
            var tableaus = new List<PileState>();
            for (int i = 0; i < rule.TableauCount; i++)
                tableaus.Add(new PileState(new PileId(PileType.Tableau, i), new List<PlayingCard>(), 0));

            var stock = new PileState(new PileId(PileType.Stock, 0), new List<PlayingCard>(), 0);
            var waste = new PileState(new PileId(PileType.Waste, 0), new List<PlayingCard>(), 0);

            var suits = new[] { Suit.Spade, Suit.Heart, Suit.Diamond, Suit.Club };
            var foundations = new List<PileState>();
            for (int f = 0; f < rule.FoundationCount; f++)
            {
                var cards = new List<PlayingCard>();
                for (int rank = 1; rank <= rule.PerSuitCardCount; rank++)
                    cards.Add(new PlayingCard((Rank)rank, suits[f]));
                foundations.Add(new PileState(new PileId(PileType.Foundation, f), cards, 0));
            }

            return new TableState(stock, waste, foundations, tableaus);
        }
    }
}
