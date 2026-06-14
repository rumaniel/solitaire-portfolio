using NUnit.Framework;
using Model.Game;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class EasthavenGameServiceTests
    {
        private SolitaireGameService _service;
        private StubDealRule _rule;

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
            _service = new SolitaireGameService(new ShuffleStrategyProvider());
            _service.Initialize(_rule, seed: 42);
        }

        [TearDown]
        public void TearDown() => _service.Dispose();

        [Test]
        public void DrawFromStock_DistributesOneCardToEachTableau()
        {
            var before = _service.CurrentState;
            int stockBefore = before.Stock.Cards.Count;
            var tableauCountsBefore = new int[before.Tableaus.Count];
            for (int i = 0; i < before.Tableaus.Count; i++)
                tableauCountsBefore[i] = before.Tableaus[i].Cards.Count;

            _service.DrawFromStock();

            var after = _service.CurrentState;
            Assert.AreEqual(stockBefore - 7, after.Stock.Cards.Count,
                "Stock should decrease by tableau count (7)");
            for (int i = 0; i < after.Tableaus.Count; i++)
                Assert.AreEqual(tableauCountsBefore[i] + 1, after.Tableaus[i].Cards.Count,
                    $"Tableau {i} should gain exactly one card");
        }

        [Test]
        public void DrawFromStock_NewlyDealtCards_AreFaceUp()
        {
            _service.DrawFromStock();
            foreach (var t in _service.CurrentState.Tableaus)
            {
                int topIdx = t.Cards.Count - 1;
                Assert.IsTrue(t.IsFaceUp(topIdx),
                    $"Tableau {t.Id.Index} newly-dealt top card should be face-up");
            }
        }

        [Test]
        public void DrawFromStock_DoesNotTouchWaste()
        {
            var wasteBefore = _service.CurrentState.Waste.Cards.Count;
            _service.DrawFromStock();
            Assert.AreEqual(wasteBefore, _service.CurrentState.Waste.Cards.Count);
        }

        [Test]
        public void DrawFromStock_PushesHistory()
        {
            Assert.IsFalse(_service.CanUndo);
            _service.DrawFromStock();
            Assert.IsTrue(_service.CanUndo);
        }

        [Test]
        public void DrawFromStock_PartialDeal_WhenStockLessThanTableauCount()
        {
            // Drain stock down to 3 cards via repeated draws (31 → 24 → 17 → 10 → 3)
            _service.DrawFromStock();
            _service.DrawFromStock();
            _service.DrawFromStock();
            _service.DrawFromStock();
            Assert.AreEqual(3, _service.CurrentState.Stock.Cards.Count);

            var tableausBefore = new int[_service.CurrentState.Tableaus.Count];
            for (int i = 0; i < tableausBefore.Length; i++)
                tableausBefore[i] = _service.CurrentState.Tableaus[i].Cards.Count;

            _service.DrawFromStock();

            Assert.AreEqual(0, _service.CurrentState.Stock.Cards.Count,
                "All remaining stock cards should be dealt");
            int dealt = 0;
            for (int i = 0; i < tableausBefore.Length; i++)
            {
                int delta = _service.CurrentState.Tableaus[i].Cards.Count - tableausBefore[i];
                Assert.IsTrue(delta == 0 || delta == 1, $"Tableau {i} delta must be 0 or 1");
                dealt += delta;
            }
            Assert.AreEqual(3, dealt, "Exactly 3 cards should have been dealt");
        }

        [Test]
        public void DrawFromStock_EmptyStock_IsNoOp()
        {
            // Drain entirely (31 / 7 = 4 full rounds + 1 partial of 3)
            for (int i = 0; i < 5; i++)
                _service.DrawFromStock();
            Assert.AreEqual(0, _service.CurrentState.Stock.Cards.Count);

            var snapshot = _service.CurrentState;
            int historyDepthBefore = 5;

            _service.DrawFromStock();

            Assert.AreSame(snapshot, _service.CurrentState,
                "Empty-stock draw must not change CurrentState");
            Assert.AreEqual(historyDepthBefore, _service.UndoHistory.Count,
                "Empty-stock draw must not push history");
        }
    }
}
