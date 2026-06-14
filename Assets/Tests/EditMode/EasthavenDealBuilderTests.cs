using System.Linq;
using NUnit.Framework;
using Model.Card;
using Model.Game;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class EasthavenDealBuilderTests
    {
        private StubDealRule _rule;
        private TableState _state;

        [SetUp]
        public void SetUp()
        {
            _rule = new StubDealRule
            {
                TableauCount = 7,
                FoundationCount = 4,
                PerSuitCardCount = 13,
                HasWaste = false,
                CanRecycleStock = false,
                StockDrawCount = 0,
                StockDealsToTableau = true,
                InitialCardCounts = new[] { 3, 3, 3, 3, 3, 3, 3 },
                InitialFaceUpPerColumn = 3,
                OnlyKingOnEmptyTableau = false,
            };
            var deck = DeckFactory.CreateShuffled(42);
            _state = DealBuilder.Build(deck, _rule);
        }

        [Test]
        public void Build_TableauCount_Is7()
        {
            Assert.AreEqual(7, _state.Tableaus.Count);
        }

        [Test]
        public void Build_EachTableau_Has3Cards()
        {
            foreach (var t in _state.Tableaus)
                Assert.AreEqual(3, t.Cards.Count);
        }

        [Test]
        public void Build_AllTableauCards_AreFaceUp()
        {
            foreach (var t in _state.Tableaus)
                for (int i = 0; i < t.Cards.Count; i++)
                    Assert.IsTrue(t.IsFaceUp(i),
                        $"Tableau {t.Id.Index} index {i} should be face-up");
        }

        [Test]
        public void Build_Stock_Has31FaceDownCards()
        {
            Assert.AreEqual(52 - 21, _state.Stock.Cards.Count);
            for (int i = 0; i < _state.Stock.Cards.Count; i++)
                Assert.IsFalse(_state.Stock.IsFaceUp(i),
                    $"Stock index {i} should be face-down");
        }

        [Test]
        public void Build_WasteIsEmpty()
        {
            Assert.AreEqual(0, _state.Waste.Cards.Count);
        }

        [Test]
        public void Build_FoundationsAreEmpty()
        {
            Assert.AreEqual(4, _state.Foundations.Count);
            foreach (var f in _state.Foundations)
                Assert.AreEqual(0, f.Cards.Count);
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
    }
}
