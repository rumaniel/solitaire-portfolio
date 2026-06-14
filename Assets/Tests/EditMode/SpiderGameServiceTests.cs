using System.Collections.Generic;
using System.Linq;
using Model.Card;
using Model.Game;
using NUnit.Framework;
using Service.CardService;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class SpiderGameServiceTests
    {
        private SolitaireGameService service;
        private StubDealRule rule;

        [SetUp]
        public void SetUp()
        {
            service = new SolitaireGameService(new ShuffleStrategyProvider());
            rule = new StubDealRule
            {
                TableauCount = 3,
                FoundationCount = 2,
                HasWaste = false,
                CanRecycleStock = false,
                StockDealsToTableau = true,
                OnlyKingOnEmptyTableau = false,
                RunRuleOverride = TableauRunRule.SameSuit,
                DropRuleOverride = TableauDropRule.AnySuit,
                StockDealRequiresNoEmptyColumnOverride = true,
                AutoCollectCompletedRunsOverride = true,
            };
        }

        private static PlayingCard S(int rank) => new PlayingCard((Rank)rank, Suit.Spade);

        private static PileState Tableau(int index, int faceUpFrom, params PlayingCard[] cards)
            => new PileState(new PileId(PileType.Tableau, index), cards.ToList(), faceUpFrom);

        private static TableState State(IReadOnlyList<PlayingCard> stockCards, params PileState[] tableaus)
            => new TableState(
                new PileState(new PileId(PileType.Stock, 0), stockCards.ToList(), stockCards.Count),
                new PileState(new PileId(PileType.Waste, 0), new List<PlayingCard>(), 0),
                new List<PileState>
                {
                    new PileState(new PileId(PileType.Foundation, 0), new List<PlayingCard>(), 0),
                    new PileState(new PileId(PileType.Foundation, 1), new List<PlayingCard>(), 0),
                },
                tableaus.ToList(), 0);

        [Test]
        public void CanDealStock_FalseWhenAnyTableauEmpty()
        {
            service.Restore(rule, 1, State(new[] { S(1), S(2), S(3) },
                Tableau(0, 0, S(5)), Tableau(1, 0), Tableau(2, 0, S(9))), new List<TableState>());
            Assert.IsFalse(service.CanDealStock);
        }

        [Test]
        public void CanDealStock_TrueWhenAllColumnsFilled()
        {
            service.Restore(rule, 1, State(new[] { S(1), S(2), S(3) },
                Tableau(0, 0, S(5)), Tableau(1, 0, S(7)), Tableau(2, 0, S(9))), new List<TableState>());
            Assert.IsTrue(service.CanDealStock);
        }

        [Test]
        public void CanDealStock_FalseWhenStockEmpty()
        {
            service.Restore(rule, 1, State(new PlayingCard[0],
                Tableau(0, 0, S(5)), Tableau(1, 0, S(7)), Tableau(2, 0, S(9))), new List<TableState>());
            Assert.IsFalse(service.CanDealStock);
        }

        [Test]
        public void DrawFromStock_EmptyColumnGuard_NoOpAndNoHistory()
        {
            service.Restore(rule, 1, State(new[] { S(1), S(2), S(3) },
                Tableau(0, 0, S(5)), Tableau(1, 0), Tableau(2, 0, S(9))), new List<TableState>());
            var before = service.CurrentState;
            service.DrawFromStock();
            Assert.AreSame(before, service.CurrentState);
            Assert.IsFalse(service.CanUndo);
        }

        [Test]
        public void ExecuteMove_CompletingRun_AutoCollects_AtomicUndo()
        {
            // tableau0: face-up K..2 (12 cards); tableau1: lone A. Moving A onto the 2 completes K..A.
            var kTo2 = Enumerable.Range(2, 12).Reverse().Select(S).ToArray();
            service.Restore(rule, 1, State(new[] { S(9) },
                Tableau(0, 0, kTo2), Tableau(1, 0, S(1)), Tableau(2, 0, S(9))), new List<TableState>());

            var request = new MoveCardRequest(S(1), new PileId(PileType.Tableau, 1), 0,
                new PileId(PileType.Tableau, 0), 1);
            service.ExecuteMove(request);

            Assert.AreEqual(13, service.CurrentState.Foundations[0].Cards.Count, "run collected");
            Assert.AreEqual(0, service.CurrentState.Tableaus[0].Cards.Count, "source column emptied");
            Assert.AreEqual(0, service.CurrentState.Tableaus[1].Cards.Count, "ace moved away");

            service.Undo();
            Assert.AreEqual(12, service.CurrentState.Tableaus[0].Cards.Count, "single undo restores pre-move");
            Assert.AreEqual(1, service.CurrentState.Tableaus[1].Cards.Count);
            Assert.AreEqual(0, service.CurrentState.Foundations[0].Cards.Count);
        }

        [Test]
        public void CollectedRun_RevealsFaceDownCardBeneath()
        {
            // tableau0: one face-down S(9) under face-up K..2 (faceUpFrom = 1).
            var cards = new List<PlayingCard> { S(9) };
            cards.AddRange(Enumerable.Range(2, 12).Reverse().Select(S));
            service.Restore(rule, 1, State(new[] { S(9) },
                new PileState(new PileId(PileType.Tableau, 0), cards, 1),
                Tableau(1, 0, S(1)), Tableau(2, 0, S(9))), new List<TableState>());

            var request = new MoveCardRequest(S(1), new PileId(PileType.Tableau, 1), 0,
                new PileId(PileType.Tableau, 0), 1);
            service.ExecuteMove(request);

            Assert.AreEqual(13, service.CurrentState.Foundations[0].Cards.Count);
            Assert.AreEqual(1, service.CurrentState.Tableaus[0].Cards.Count);
            Assert.IsTrue(service.CurrentState.Tableaus[0].IsFaceUp(0), "card beneath flips face-up");
        }

        [Test]
        public void DealStock_CompletingRun_AutoCollects()
        {
            // Dealing sends stock TOP card to tableau 0 first: top is the Ace.
            var kTo2 = Enumerable.Range(2, 12).Reverse().Select(S).ToArray();
            service.Restore(rule, 1, State(new[] { S(9), S(9), S(1) }, // deal order: A->t0, 9->t1, 9->t2
                Tableau(0, 0, kTo2), Tableau(1, 0, S(4)), Tableau(2, 0, S(6))), new List<TableState>());

            service.DrawFromStock();

            Assert.AreEqual(13, service.CurrentState.Foundations[0].Cards.Count);
            Assert.AreEqual(0, service.CurrentState.Tableaus[0].Cards.Count);
            Assert.AreEqual(0, service.CurrentState.Stock.Cards.Count);
        }

        [Test]
        public void Initialize_SpiderRule_DealsFullLayoutWithoutThrowing()
        {
            // Restore() bypasses the shuffle path; this guards the REAL entry point —
            // a 52-card deck here means Spider cannot start at all.
            var spiderRule = new StubDealRule
            {
                TableauCount = 10, FoundationCount = 8,
                HasWaste = false, CanRecycleStock = false, StockDealsToTableau = true,
                OnlyKingOnEmptyTableau = false,
                InitialCardCounts = new[] { 6, 6, 6, 6, 5, 5, 5, 5, 5, 5 },
                InitialFaceUpPerColumn = 1,
                DeckCountOverride = 2, SuitCountOverride = 1,
                RunRuleOverride = TableauRunRule.SameSuit,
                DropRuleOverride = TableauDropRule.AnySuit,
                StockDealRequiresNoEmptyColumnOverride = true,
                AutoCollectCompletedRunsOverride = true,
            };
            service.Initialize(spiderRule, 1234);

            Assert.AreEqual(54, service.CurrentState.Tableaus.Sum(t => t.Cards.Count));
            Assert.AreEqual(50, service.CurrentState.Stock.Cards.Count);
        }

        [Test]
        public void IsWon_AllFoundationsFull()
        {
            var fullRun = Enumerable.Range(1, 13).Reverse().Select(S).ToList(); // K..A
            var state = new TableState(
                new PileState(new PileId(PileType.Stock, 0), new List<PlayingCard>(), 0),
                new PileState(new PileId(PileType.Waste, 0), new List<PlayingCard>(), 0),
                new List<PileState>
                {
                    new PileState(new PileId(PileType.Foundation, 0), fullRun, 0),
                    new PileState(new PileId(PileType.Foundation, 1), fullRun.ToList(), 0),
                },
                new List<PileState> { Tableau(0, 0), Tableau(1, 0), Tableau(2, 0) }, 0);
            service.Restore(rule, 1, state, new List<TableState>());
            Assert.IsTrue(service.IsWon(service.CurrentState));
        }
    }
}
