using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Model.Game;
using Model.Card;
using Service.CardService;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class SolitaireGameServiceTests
    {
        private SolitaireGameService _service;
        private StubDealRule _rule;

        [SetUp]
        public void SetUp()
        {
            _rule = new StubDealRule();
            _service = new SolitaireGameService(new ShuffleStrategyProvider());
            _service.Initialize(_rule);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        // --- Initialize ---

        [Test]
        public void Initialize_TableauCount_MatchesRule()
        {
            Assert.AreEqual(_rule.TableauCount, _service.CurrentState.Tableaus.Count);
        }

        [Test]
        public void Initialize_TotalCardCount_MatchesRule()
        {
            var state = _service.CurrentState;
            var total = state.Stock.Cards.Count
                + state.Waste.Cards.Count
                + state.Foundations.Sum(f => f.Cards.Count)
                + state.Tableaus.Sum(t => t.Cards.Count);
            var expected = Enum.GetValues(typeof(Suit)).Cast<Suit>().Where(s => s != Suit.None).Count() * _rule.PerSuitCardCount;
            Assert.AreEqual(expected, total);
        }

        [Test]
        public void Initialize_FoundationsAreEmpty()
        {
            foreach (var f in _service.CurrentState.Foundations)
                Assert.AreEqual(0, f.Cards.Count);
        }

        [Test]
        public void Initialize_WasteIsEmpty()
        {
            Assert.AreEqual(0, _service.CurrentState.Waste.Cards.Count);
        }

        [Test]
        public void Initialize_CanUndo_IsFalse()
        {
            Assert.IsFalse(_service.CanUndo);
        }

        [Test]
        public void Initialize_WithSeed_StoresSeed()
        {
            _service.Initialize(_rule, 12345);
            Assert.AreEqual(12345, _service.CurrentSeed.Value);
        }

        [Test]
        public void Initialize_WithoutSeed_StoresUsableSeed()
        {
            // Initialize without explicit seed, capture the generated seed,
            // then re-initialize with that seed and verify identical layout.
            // This deterministically proves the random seed is stored and used.
            _service.Initialize(_rule);
            int generatedSeed = _service.CurrentSeed.Value;
            var state1 = _service.CurrentState;

            _service.Initialize(_rule, generatedSeed);
            var state2 = _service.CurrentState;

            Assert.AreEqual(state1.Stock.Cards.Count, state2.Stock.Cards.Count);
            for (int i = 0; i < state1.Stock.Cards.Count; i++)
                Assert.AreEqual(state1.Stock.Cards[i], state2.Stock.Cards[i], $"Stock index {i} mismatch");

            for (int col = 0; col < _rule.TableauCount; col++)
            {
                var t1 = state1.Tableaus[col];
                var t2 = state2.Tableaus[col];
                Assert.AreEqual(t1.Cards.Count, t2.Cards.Count, $"Tableau {col} count mismatch");
                for (int i = 0; i < t1.Cards.Count; i++)
                    Assert.AreEqual(t1.Cards[i], t2.Cards[i], $"Tableau {col}, index {i} mismatch");
            }
        }

        [Test]
        public void Initialize_SameSeed_SameLayout()
        {
            _service.Initialize(_rule, 42);
            var state1 = _service.CurrentState;

            _service.Initialize(_rule, 42);
            var state2 = _service.CurrentState;

            // Compare all tableau cards
            for (int col = 0; col < _rule.TableauCount; col++)
            {
                var t1 = state1.Tableaus[col];
                var t2 = state2.Tableaus[col];
                Assert.AreEqual(t1.Cards.Count, t2.Cards.Count, $"Tableau {col} count mismatch");
                for (int i = 0; i < t1.Cards.Count; i++)
                    Assert.AreEqual(t1.Cards[i], t2.Cards[i], $"Tableau {col}, index {i} mismatch");
            }

            // Compare stock
            Assert.AreEqual(state1.Stock.Cards.Count, state2.Stock.Cards.Count);
            for (int i = 0; i < state1.Stock.Cards.Count; i++)
                Assert.AreEqual(state1.Stock.Cards[i], state2.Stock.Cards[i], $"Stock index {i} mismatch");
        }

        // --- DrawFromStock ---

        [Test]
        public void DrawFromStock_MovesTopCardToWaste()
        {
            int stockBefore = _service.CurrentState.Stock.Cards.Count;
            int wasteBefore = _service.CurrentState.Waste.Cards.Count;
            _service.DrawFromStock();
            Assert.AreEqual(stockBefore - 1, _service.CurrentState.Stock.Cards.Count);
            Assert.AreEqual(wasteBefore + 1, _service.CurrentState.Waste.Cards.Count);
        }

        [Test]
        public void DrawFromStock_DrawnCard_IsFaceUp()
        {
            _service.DrawFromStock();
            var waste = _service.CurrentState.Waste;
            Assert.IsTrue(waste.IsFaceUp(waste.Cards.Count - 1));
        }

        [Test]
        public void DrawFromStock_EmptyStock_CanRecycle_RestoresWasteToStock()
        {
            int stockSize = _service.CurrentState.Stock.Cards.Count;
            for (int i = 0; i < stockSize; i++)
                _service.DrawFromStock();

            int wasteSize = _service.CurrentState.Waste.Cards.Count;
            _service.DrawFromStock(); // triggers recycle

            Assert.AreEqual(wasteSize, _service.CurrentState.Stock.Cards.Count);
            Assert.AreEqual(0, _service.CurrentState.Waste.Cards.Count);
        }

        [Test]
        public void DrawFromStock_EmptyStock_CannotRecycle_DoesNothing()
        {
            _rule.CanRecycleStock = false;
            _service.Initialize(_rule);

            int stockSize = _service.CurrentState.Stock.Cards.Count;
            for (int i = 0; i < stockSize; i++)
                _service.DrawFromStock();

            int stockBefore = _service.CurrentState.Stock.Cards.Count;
            int wasteBefore = _service.CurrentState.Waste.Cards.Count;
            _service.DrawFromStock(); // should do nothing

            Assert.AreEqual(stockBefore, _service.CurrentState.Stock.Cards.Count);
            Assert.AreEqual(wasteBefore, _service.CurrentState.Waste.Cards.Count);
        }

        // --- ExecuteMove ---

        [Test]
        public void ExecuteMove_RemovesCardFromSource_AddsToTarget()
        {
            var source = _service.CurrentState.Tableaus[0];
            var target = _service.CurrentState.Tableaus[1];
            int srcCount = source.Cards.Count;
            int tgtCount = target.Cards.Count;

            var req = new MoveCardRequest(source.Cards[srcCount - 1], source.Id, srcCount - 1, target.Id);
            _service.ExecuteMove(req);

            Assert.AreEqual(srcCount - 1, _service.CurrentState.Tableaus[0].Cards.Count);
            Assert.AreEqual(tgtCount + 1, _service.CurrentState.Tableaus[1].Cards.Count);
        }

        [Test]
        public void ExecuteMove_FlipOnReveal_NewTopBecomesFaceUp()
        {
            // Tableau[1] starts with 2 cards: index 0 face-down, index 1 face-up
            var tableau1 = _service.CurrentState.Tableaus[1];
            Assert.AreEqual(2, tableau1.Cards.Count);
            Assert.AreEqual(1, tableau1.FaceUpFromIndex);

            var req = new MoveCardRequest(tableau1.Cards[1], tableau1.Id, 1, _service.CurrentState.Waste.Id);
            _service.ExecuteMove(req);

            var updated = _service.CurrentState.Tableaus[1];
            Assert.AreEqual(1, updated.Cards.Count);
            Assert.IsTrue(updated.IsFaceUp(0));
        }

        // --- Undo ---

        [Test]
        public void Undo_RestoresPreviousState()
        {
            var stateBefore = _service.CurrentState;

            var stockBefore = stateBefore.Stock.Cards.ToList();
            var wasteBefore = stateBefore.Waste.Cards.ToList();
            _service.DrawFromStock();
            _service.Undo();

            var stateAfter = _service.CurrentState;

            CollectionAssert.AreEqual(stockBefore, stateAfter.Stock.Cards);
            CollectionAssert.AreEqual(wasteBefore, stateAfter.Waste.Cards);
        }

        [Test]
        public void CanUndo_AfterMove_IsTrue()
        {
            _service.DrawFromStock();
            Assert.IsTrue(_service.CanUndo);
        }

        [Test]
        public void CanUndo_AfterUndoingAll_IsFalse()
        {
            _service.DrawFromStock();
            _service.Undo();
            Assert.IsFalse(_service.CanUndo);
        }

        // --- WasteFanCount ---

        [Test]
        public void Initialize_WasteFanCount_IsZero()
        {
            Assert.AreEqual(0, _service.CurrentState.WasteFanCount);
        }

        [Test]
        public void DrawFromStock_Draw1_FanCountIsOne()
        {
            _service.DrawFromStock();
            Assert.AreEqual(1, _service.CurrentState.WasteFanCount);
        }

        [Test]
        public void DrawFromStock_Draw3_FanCountEqualsDrawn()
        {
            _rule.StockDrawCount = 3;
            _service.Initialize(_rule);

            _service.DrawFromStock();
            Assert.AreEqual(3, _service.CurrentState.WasteFanCount);
        }

        [Test]
        public void DrawFromStock_Draw3_LessThan3InStock_FanCountEqualsRemaining()
        {
            // Use fewer tableau columns so stock has a non-multiple-of-3 count
            _rule.StockDrawCount = 3;
            _rule.InitialCardCounts = new[] { 1, 2, 3, 4, 5, 6 }; // 6 tableaus → 21 dealt, 31 in stock
            _rule.TableauCount = 6;
            _service.Initialize(_rule);

            // Draw until fewer than 3 cards remain in stock
            while (_service.CurrentState.Stock.Cards.Count >= 3)
                _service.DrawFromStock();

            int remaining = _service.CurrentState.Stock.Cards.Count;
            Assert.IsTrue(remaining > 0 && remaining < 3, $"Expected 1-2 remaining, got {remaining}");

            _service.DrawFromStock();
            Assert.AreEqual(remaining, _service.CurrentState.WasteFanCount);
        }

        [Test]
        public void DrawFromStock_Recycle_FanCountResets()
        {
            _rule.StockDrawCount = 3;
            _service.Initialize(_rule);

            // Draw all cards from stock
            while (_service.CurrentState.Stock.Cards.Count > 0)
                _service.DrawFromStock();

            Assert.IsTrue(_service.CurrentState.WasteFanCount > 0);

            // Recycle
            _service.DrawFromStock();
            Assert.AreEqual(0, _service.CurrentState.WasteFanCount);
        }

        [Test]
        public void ExecuteMove_FromWaste_DecrementsFanCount()
        {
            _rule.StockDrawCount = 3;
            _service.Initialize(_rule);
            _service.DrawFromStock(); // fan = 3

            // Move top waste card to a tableau
            var waste = _service.CurrentState.Waste;
            var target = _service.CurrentState.Tableaus[0];
            var req = new MoveCardRequest(
                waste.Cards[waste.Cards.Count - 1],
                waste.Id, waste.Cards.Count - 1,
                target.Id);
            _service.ExecuteMove(req);

            Assert.AreEqual(2, _service.CurrentState.WasteFanCount);
        }

        [Test]
        public void ExecuteMove_NotFromWaste_FanCountUnchanged()
        {
            _rule.StockDrawCount = 3;
            _service.Initialize(_rule);
            _service.DrawFromStock(); // fan = 3
            int fanBefore = _service.CurrentState.WasteFanCount;

            // Move between tableaus (not from waste)
            var source = _service.CurrentState.Tableaus[0];
            var target = _service.CurrentState.Tableaus[1];
            var req = new MoveCardRequest(
                source.Cards[source.Cards.Count - 1],
                source.Id, source.Cards.Count - 1,
                target.Id);
            _service.ExecuteMove(req);

            Assert.AreEqual(fanBefore, _service.CurrentState.WasteFanCount);
        }

        [Test]
        public void Undo_RestoresWasteFanCount()
        {
            _rule.StockDrawCount = 3;
            _service.Initialize(_rule);
            _service.DrawFromStock(); // fan = 3

            var waste = _service.CurrentState.Waste;
            var target = _service.CurrentState.Tableaus[0];
            var req = new MoveCardRequest(
                waste.Cards[waste.Cards.Count - 1],
                waste.Id, waste.Cards.Count - 1,
                target.Id);
            _service.ExecuteMove(req); // fan = 2

            Assert.AreEqual(2, _service.CurrentState.WasteFanCount);

            _service.Undo(); // should restore fan = 3
            Assert.AreEqual(3, _service.CurrentState.WasteFanCount);
        }

        [Test]
        public void ExecuteMove_FromWaste_FanCountClampedAtZero()
        {
            // Draw 1 → fan = 1, move that card → fan = 0 (not negative)
            _service.DrawFromStock(); // fan = 1

            var waste = _service.CurrentState.Waste;
            var target = _service.CurrentState.Tableaus[0];
            var req = new MoveCardRequest(
                waste.Cards[waste.Cards.Count - 1],
                waste.Id, waste.Cards.Count - 1,
                target.Id);
            _service.ExecuteMove(req);

            Assert.AreEqual(0, _service.CurrentState.WasteFanCount);
        }
    }
}
