using System.Linq;
using NUnit.Framework;
using Model.Card;
using Model.Game;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class DealBuilderTests
    {
        private StubDealRule _rule;
        private TableState _state;

        [SetUp]
        public void SetUp()
        {
            _rule = new StubDealRule();
            var deck = DeckFactory.CreateShuffled(42);
            _state = DealBuilder.Build(deck, _rule);
        }

        [Test]
        public void Build_TableauCount_MatchesRule()
        {
            Assert.AreEqual(_rule.TableauCount, _state.Tableaus.Count);
        }

        [Test]
        public void Build_FoundationCount_MatchesRule()
        {
            Assert.AreEqual(_rule.FoundationCount, _state.Foundations.Count);
        }

        [Test]
        public void Build_FoundationsAreEmpty()
        {
            foreach (var f in _state.Foundations)
                Assert.AreEqual(0, f.Cards.Count);
        }

        [Test]
        public void Build_WasteIsEmpty()
        {
            Assert.AreEqual(0, _state.Waste.Cards.Count);
        }

        [Test]
        public void Build_TableauCardCounts_MatchRule()
        {
            for (int i = 0; i < _rule.TableauCount; i++)
                Assert.AreEqual(_rule.InitialCardCounts[i], _state.Tableaus[i].Cards.Count);
        }

        [Test]
        public void Build_StockGetsRemainingCards()
        {
            int tableauTotal = _rule.InitialCardCounts.Sum();
            Assert.AreEqual(52 - tableauTotal, _state.Stock.Cards.Count);
        }

        [Test]
        public void Build_TotalCardCount_Is52()
        {
            var total = _state.Stock.Cards.Count
                + _state.Waste.Cards.Count
                + _state.Foundations.Sum(f => f.Cards.Count)
                + _state.Tableaus.Sum(t => t.Cards.Count);
            Assert.AreEqual(52, total);
        }

        [Test]
        public void Build_TableauFaceUp_LastCardOnly()
        {
            for (int col = 0; col < _rule.TableauCount; col++)
            {
                var pile = _state.Tableaus[col];
                if (pile.Cards.Count == 0) continue;

                // Last card should be face-up
                Assert.IsTrue(pile.IsFaceUp(pile.Cards.Count - 1),
                    $"Tableau {col}: top card should be face-up");

                // All other cards should be face-down
                for (int i = 0; i < pile.Cards.Count - 1; i++)
                    Assert.IsFalse(pile.IsFaceUp(i),
                        $"Tableau {col}, index {i}: should be face-down");
            }
        }

        [Test]
        public void Build_StockCardsAllFaceDown()
        {
            for (int i = 0; i < _state.Stock.Cards.Count; i++)
                Assert.IsFalse(_state.Stock.IsFaceUp(i), $"Stock index {i} should be face-down");
        }

        [Test]
        public void Build_SpiderRule_DealsFiftyFourTableau_FiftyStock()
        {
            var rule = new StubDealRule
            {
                TableauCount = 10,
                FoundationCount = 8,
                HasWaste = false,
                StockDealsToTableau = true,
                InitialCardCounts = new[] { 6, 6, 6, 6, 5, 5, 5, 5, 5, 5 },
                InitialFaceUpPerColumn = 1,
                OnlyKingOnEmptyTableau = false,
                DeckCountOverride = 2,
                SuitCountOverride = 1,
            };
            var deck = DeckFactory.CreateShuffled(11, rule);
            var state = DealBuilder.Build(deck, rule);

            Assert.AreEqual(10, state.Tableaus.Count);
            Assert.AreEqual(8, state.Foundations.Count);
            Assert.AreEqual(54, state.Tableaus.Sum(t => t.Cards.Count));
            Assert.AreEqual(50, state.Stock.Cards.Count);
            foreach (var t in state.Tableaus)
                Assert.AreEqual(t.Cards.Count - 1, t.FaceUpFromIndex, "only the top card is face-up");
        }
    }
}
