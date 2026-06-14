using System.Collections.Generic;
using NUnit.Framework;
using Model.Card;
using Model.Game;
using Service.CardService;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class KlondikeSolverTests
    {
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
        public void Solve_AlreadySolvedState_ReturnsSolvedImmediately()
        {
            var state = BuildFullySolved(_rule);
            var result = KlondikeSolver.Solve(state, _cardService, _rule, stateBudget: 100);

            Assert.IsTrue(result.Solved);
            Assert.IsFalse(result.BudgetExceeded);
        }

        [Test, Explicit("Runs solver DFS — expensive; opt-in only")]
        public void Solve_SolvedShuffleStrategyBaseline_IsWinnable()
        {
            // SolvedShuffleStrategy(extraShuffles=0) is explicitly constructed as a
            // trivially-solvable baseline. Solver should find a win quickly.
            var deck = new SolvedShuffleStrategy(extraShuffles: 0).Shuffle(seed: 42);
            var state = DealBuilder.Build(deck, _rule);
            var result = KlondikeSolver.Solve(state, _cardService, _rule, stateBudget: 10_000);

            Assert.IsTrue(result.Solved,
                $"SolvedShuffleStrategy baseline must be solvable. Explored {result.StatesExplored} states, budgetExceeded={result.BudgetExceeded}");
        }

        /// <summary>
        /// Builds a TableState whose foundations already hold A-K of each suit —
        /// the trivial win position used by the solver as its termination check.
        /// </summary>
        private static TableState BuildFullySolved(IDealRule rule)
        {
            var tableaus = new List<PileState>();
            for (int i = 0; i < rule.TableauCount; i++)
                tableaus.Add(new PileState(new PileId(PileType.Tableau, i), new List<PlayingCard>(), 0));

            var stock = new PileState(new PileId(PileType.Stock, 0), new List<PlayingCard>(), 0);
            var waste = new PileState(new PileId(PileType.Waste, 0), new List<PlayingCard>(), 0);

            var suits = new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond };
            var foundations = new List<PileState>();
            for (int i = 0; i < rule.FoundationCount; i++)
            {
                var cards = new List<PlayingCard>();
                for (int r = (int)Rank.Ace; r <= (int)Rank.King; r++)
                    cards.Add(new PlayingCard((Rank)r, suits[i]));
                foundations.Add(new PileState(new PileId(PileType.Foundation, i), cards, 0));
            }

            return new TableState(stock, waste, foundations, tableaus);
        }
    }
}
