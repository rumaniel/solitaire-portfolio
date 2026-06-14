using System;
using System.Collections.Generic;
using System.Linq;
using Model.Board;
using Model.Card;
using NUnit.Framework;
using R3;
using Service.BoardGameService;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class PyramidGameServiceTests
    {
        private PyramidGameService NewService()
            => new PyramidGameService(new FisherYatesShuffleStrategy());

        private PyramidGameService NewInitializedPyramid(int seed = 12345)
        {
            var svc = NewService();
            svc.Initialize(PyramidLayoutFactory.Create(), new PyramidMatchRule(), seed);
            return svc;
        }

        [Test]
        public void Restore_ThrowsOnNullState()
        {
            var svc = NewService();
            Assert.Throws<ArgumentNullException>(() =>
                svc.Restore(PyramidLayoutFactory.Create(), new PyramidMatchRule(), 1,
                    state: null, undoHistory: new List<BoardState>()));
        }

        [Test]
        public void Restore_ThrowsOnCellCountMismatch()
        {
            var svc = NewService();
            var oneCellState = new BoardState(new[] { new PlayingCard(Rank.Ace, Suit.Spade) }); // 1 cell
            Assert.Throws<ArgumentException>(() =>
                svc.Restore(PyramidLayoutFactory.Create() /* 28 cells */, new PyramidMatchRule(), 1,
                    oneCellState, new List<BoardState>()));
        }

        [Test]
        public void Restore_OnInvalidSnapshot_LeavesServiceUnchanged()
        {
            // A running game...
            var svc = NewInitializedPyramid(seed: 7);
            var layoutBefore = svc.Layout;
            var stateBefore = svc.CurrentState;
            var seedBefore = svc.CurrentSeed;

            // ...is asked to restore an incompatible snapshot (1-cell state into a 28-cell layout).
            var badState = new BoardState(new[] { new PlayingCard(Rank.Ace, Suit.Spade) });
            Assert.Throws<ArgumentException>(() =>
                svc.Restore(PyramidLayoutFactory.Create(), new PyramidMatchRule(), 99,
                    badState, new List<BoardState>()));

            // Restore must be atomic: nothing changed.
            Assert.AreSame(layoutBefore, svc.Layout);
            Assert.AreSame(stateBefore, svc.CurrentState);
            Assert.AreEqual(seedBefore, svc.CurrentSeed);
        }

        [Test]
        public void SelectCell_UnknownId_IsIgnored()
        {
            var svc = NewInitializedPyramid();
            // CellId not in the layout → ignored per the API doc, must not throw.
            Assert.DoesNotThrow(() => svc.SelectCell(new CellId(9999)));
        }

        [Test]
        public void Initialize_Deals28ToCells_24ToStock_Deterministically()
        {
            var a = NewInitializedPyramid(seed: 99);
            var b = NewInitializedPyramid(seed: 99);

            Assert.AreEqual(28, a.CurrentState.OccupiedCells().Count());
            Assert.AreEqual(24, a.CurrentState.Stock.Count);
            Assert.AreEqual(0, a.CurrentState.Waste.Count);
            Assert.AreEqual(99, a.CurrentSeed);
            // same seed → identical deal
            Assert.IsTrue(a.CurrentState.Equals(b.CurrentState));
        }

        // Build a tiny controllable game: a 3-cell single-layer layout, no covers, with chosen cards.
        private static BoardLayout FlatLayout(int n)
        {
            var cells = new System.Collections.Generic.List<BoardCell>(n);
            for (int i = 0; i < n; i++)
                cells.Add(new BoardCell(new CellId(i), null));
            return new BoardLayout(Model.Game.GameType.None, 1, cells);
        }

        private sealed class FixedShuffle : IShuffleStrategy
        {
            private readonly System.Collections.Generic.List<PlayingCard> deck;
            public FixedShuffle(params PlayingCard[] cards) => deck = new System.Collections.Generic.List<PlayingCard>(cards);
            public System.Collections.Generic.List<PlayingCard> Shuffle(int seed)
                => new System.Collections.Generic.List<PlayingCard>(deck);
        }

        private static PlayingCard Card(Rank r) => new PlayingCard(r, Suit.Spade);

        [Test]
        public void SelectingPairSummingTo13_RemovesBothCells()
        {
            // cells: [9, 4, 2]  → 9+4 = 13
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four), Card(Rank.Two)));
            svc.Initialize(FlatLayout(3), new PyramidMatchRule(), seed: 0);

            svc.SelectCell(new CellId(0)); // 9 → incomplete
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)));
            svc.SelectCell(new CellId(1)); // 4 → match

            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(0)));
            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(1)));
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(2)));
        }

        [Test]
        public void SelectingKing_RemovesItAlone()
        {
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.King), Card(Rank.Two), Card(Rank.Three)));
            svc.Initialize(FlatLayout(3), new PyramidMatchRule(), seed: 0);

            svc.SelectCell(new CellId(0)); // King → match immediately
            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(0)));
        }

        [Test]
        public void DrawFromStock_MovesTopStockCardToWaste()
        {
            // 1 cell (King) + 1 stock card (7).
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.King), Card(Rank.Seven)));
            svc.Initialize(FlatLayout(1), new PyramidMatchRule(), seed: 0);
            Assert.AreEqual(1, svc.CurrentState.Stock.Count);
            Assert.IsNull(svc.CurrentState.WasteTop);

            svc.DrawFromStock();

            Assert.AreEqual(0, svc.CurrentState.Stock.Count);
            Assert.AreEqual(Rank.Seven, svc.CurrentState.WasteTop.Rank);
        }

        [Test]
        public void DrawFromStock_EmptyStock_IsNoOp()
        {
            // 1 card → fills the single cell, leaving an empty stock.
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.King)));
            svc.Initialize(FlatLayout(1), new PyramidMatchRule(), seed: 0);
            Assert.AreEqual(0, svc.CurrentState.Stock.Count);
            var before = svc.CurrentState;

            svc.DrawFromStock();

            Assert.AreSame(before, svc.CurrentState);
        }

        [Test]
        public void SelectWasteTop_PairsWithFreeCell_RemovesBoth()
        {
            // cell 0 = 9 (free); stock = [4]. Draw → waste-top 4. 9 + 4 = 13.
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four)));
            svc.Initialize(FlatLayout(1), new PyramidMatchRule(), seed: 0);
            svc.DrawFromStock();
            Assert.AreEqual(Rank.Four, svc.CurrentState.WasteTop.Rank);

            svc.SelectCell(new CellId(0)); // 9 → incomplete
            svc.SelectWasteTop();          // 4 → 9+4=13 → match removes cell 0 + waste-top

            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(0)));
            Assert.IsNull(svc.CurrentState.WasteTop);
        }

        [Test]
        public void InvalidPair_ThenKing_RemovesKingViaReset()
        {
            // [5, K, 3]: tap 5 (incomplete) → tap K (invalid pair) resets to {K} which matches alone
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Five), Card(Rank.King), Card(Rank.Three)));
            svc.Initialize(FlatLayout(3), new PyramidMatchRule(), seed: 0);

            svc.SelectCell(new CellId(0)); // 5
            svc.SelectCell(new CellId(1)); // K → 5+13 invalid → reset to {K} → match
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)), "5 stays");
            Assert.IsFalse(svc.CurrentState.HasCard(new CellId(1)), "K removed");
        }

        [Test]
        public void SelectingNonFreeCell_IsIgnored()
        {
            // cell 0 covered by cell 1; tapping 0 does nothing.
            var cells = new System.Collections.Generic.List<BoardCell>
            {
                new BoardCell(new CellId(0), new[] { new CellId(1) }),
                new BoardCell(new CellId(1), null),
            };
            var layout = new BoardLayout(Model.Game.GameType.None, 1, cells);
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.King), Card(Rank.Two)));
            svc.Initialize(layout, new PyramidMatchRule(), seed: 0);

            svc.SelectCell(new CellId(0)); // not free → ignored, no match
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)));
        }

        [Test]
        public void PublishesNewState_OnMatch()
        {
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four), Card(Rank.Two)));
            svc.Initialize(FlatLayout(3), new PyramidMatchRule(), seed: 0);

            int publishes = 0;
            using var sub = svc.OnBoardStateChanged.Subscribe(s => publishes++);
            svc.SelectCell(new CellId(0));
            svc.SelectCell(new CellId(1)); // match → 1 publish
            Assert.AreEqual(1, publishes);
        }

        [Test]
        public void IsWon_TrueWhenAllCellsCleared()
        {
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.King), Card(Rank.King)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            Assert.IsFalse(svc.IsWon(svc.CurrentState));

            svc.SelectCell(new CellId(0)); // King removed alone
            svc.SelectCell(new CellId(1)); // King removed alone
            Assert.IsTrue(svc.IsWon(svc.CurrentState));
        }

        [Test]
        public void HasAnyMove_TrueWhenStockNotEmpty()
        {
            // 2 cells [2,3] (no pair sums to 13), but stock has a card → draw is a move.
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Two), Card(Rank.Three), Card(Rank.Seven)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            Assert.AreEqual(1, svc.CurrentState.Stock.Count);
            Assert.IsTrue(svc.HasAnyMove(svc.CurrentState));
        }

        [Test]
        public void HasAnyMove_FalseWhenNoPairAndNoStock()
        {
            // cells [2,3], no stock → 2+3=5, no King → stuck.
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Two), Card(Rank.Three)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            Assert.IsFalse(svc.HasAnyMove(svc.CurrentState));
        }

        [Test]
        public void HasAnyMove_TrueWhenFreePairSumsTo13()
        {
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            Assert.IsTrue(svc.HasAnyMove(svc.CurrentState));
        }

        [Test]
        public void Undo_RestoresPreviousState()
        {
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four), Card(Rank.Two)));
            svc.Initialize(FlatLayout(3), new PyramidMatchRule(), seed: 0);

            Assert.IsFalse(svc.CanUndo);
            svc.SelectCell(new CellId(0));
            svc.SelectCell(new CellId(1)); // remove 9 & 4
            Assert.IsTrue(svc.CanUndo);

            svc.Undo();
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(0)));
            Assert.IsTrue(svc.CurrentState.HasCard(new CellId(1)));
            Assert.IsFalse(svc.CanUndo);
        }

        [Test]
        public void Restore_RebuildsStateSeedAndHistory()
        {
            var svc = new PyramidGameService(new FisherYatesShuffleStrategy());
            var layout = PyramidLayoutFactory.Create();
            var rule = new PyramidMatchRule();

            var source = new PyramidGameService(new FisherYatesShuffleStrategy());
            source.Initialize(layout, rule, seed: 42);
            var snapshotState = source.CurrentState;

            svc.Restore(layout, rule, seed: 42, state: snapshotState,
                undoHistory: new System.Collections.Generic.List<BoardState>());

            Assert.AreEqual(42, svc.CurrentSeed);
            Assert.IsTrue(svc.CurrentState.Equals(snapshotState));
            Assert.IsFalse(svc.CanUndo);
        }

        [Test]
        public void SelectCell_FreeCard_PublishesPendingSelection()
        {
            // cells: [9, 4] (FlatLayout = no covers → both free)
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);

            SelectionSnapshot last = null;
            using var _ = svc.OnSelectionChanged.Subscribe(s => last = s);

            svc.SelectCell(new CellId(0)); // 9 → incomplete, stays selected

            Assert.IsNotNull(last);
            Assert.IsTrue(last.Contains(new CellId(0)));
            Assert.IsFalse(last.WasteSelected);
        }

        [Test]
        public void Match_ClearsSelection()
        {
            // cells: [9, 4] → 9+4 = 13 removes both; selection must publish Empty
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);

            SelectionSnapshot last = null;
            using var _ = svc.OnSelectionChanged.Subscribe(s => last = s);

            svc.SelectCell(new CellId(0)); // 9 → incomplete
            svc.SelectCell(new CellId(1)); // 4 → match → selection cleared

            Assert.AreEqual(SelectionSnapshot.Empty, last);
        }

        [Test]
        public void CurrentSelection_ReflectsSelectAndMatchSynchronously()
        {
            // cells: [9, 4] → 9 stays pending, then 9+4 = 13 clears the selection
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);

            Assert.AreEqual(SelectionSnapshot.Empty, svc.CurrentSelection); // fresh deal: nothing selected

            svc.SelectCell(new CellId(0));                                   // 9 → incomplete, stays selected
            Assert.IsTrue(svc.CurrentSelection.Contains(new CellId(0)));
            Assert.IsFalse(svc.CurrentSelection.WasteSelected);

            svc.SelectCell(new CellId(1));                                   // 4 → match → selection cleared
            Assert.AreEqual(SelectionSnapshot.Empty, svc.CurrentSelection);
        }

        [Test]
        public void RecycleStock_WhenStockEmptyAndPassesRemain_MovesWasteBackToStock()
        {
            // 1 cell (Nine) + stock [Four, Five]; draw both to waste, then recycle.
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four), Card(Rank.Five)));
            svc.Initialize(FlatLayout(1), new PyramidMatchRule(), seed: 0, maxRecycles: 2);

            svc.DrawFromStock();
            svc.DrawFromStock();
            Assert.AreEqual(0, svc.CurrentState.Stock.Count);
            Assert.AreEqual(2, svc.CurrentState.Waste.Count);
            Assert.IsTrue(svc.CanRecycle(svc.CurrentState));

            svc.RecycleStock();
            Assert.AreEqual(2, svc.CurrentState.Stock.Count);
            Assert.AreEqual(0, svc.CurrentState.Waste.Count);
            Assert.AreEqual(1, svc.CurrentState.RecycleCount);
        }

        [Test]
        public void RecycleStock_NoOpWhenStockNotEmpty()
        {
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four)));
            svc.Initialize(FlatLayout(1), new PyramidMatchRule(), seed: 0, maxRecycles: 3);

            Assert.IsFalse(svc.CanRecycle(svc.CurrentState));   // stock still has a card
            svc.RecycleStock();
            Assert.AreEqual(0, svc.CurrentState.RecycleCount);
        }

        [Test]
        public void RecycleStock_StopsAfterMaxPasses()
        {
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four)));
            svc.Initialize(FlatLayout(1), new PyramidMatchRule(), seed: 0, maxRecycles: 1);

            svc.DrawFromStock();                                 // stock empty, waste = [Four]
            svc.RecycleStock();                                  // pass 1
            Assert.AreEqual(1, svc.CurrentState.RecycleCount);

            svc.DrawFromStock();                                 // stock empty again
            Assert.IsFalse(svc.CanRecycle(svc.CurrentState));    // limit reached
            svc.RecycleStock();                                  // no-op
            Assert.AreEqual(1, svc.CurrentState.RecycleCount);
        }

        [Test]
        public void HasAnyMove_TrueWhenRecycleAvailableEvenWithNoMatch()
        {
            // cell Two + waste Five: 2+5 != 13 and neither is a King → no match; only a recycle remains.
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Two), Card(Rank.Five)));
            svc.Initialize(FlatLayout(1), new PyramidMatchRule(), seed: 0, maxRecycles: 1);

            svc.DrawFromStock();                                 // stock empty, waste = [Five]
            Assert.IsTrue(svc.CanRecycle(svc.CurrentState));
            Assert.IsTrue(svc.HasAnyMove(svc.CurrentState));     // recycle counts as a move
        }

        [Test]
        public void RecycleStock_IsUndoable()
        {
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four)));
            svc.Initialize(FlatLayout(1), new PyramidMatchRule(), seed: 0, maxRecycles: 2);

            svc.DrawFromStock();                                 // stock empty, waste = [Four]
            svc.RecycleStock();                                  // stock = [Four], waste empty, recycle = 1
            Assert.AreEqual(1, svc.CurrentState.RecycleCount);

            svc.Undo();
            Assert.AreEqual(0, svc.CurrentState.RecycleCount);   // recycle reverted
            Assert.AreEqual(0, svc.CurrentState.Stock.Count);
            Assert.AreEqual(1, svc.CurrentState.Waste.Count);    // waste restored
        }

        [Test]
        public void BoardHint_Match_ValueEquality()
        {
            var a = BoardHint.OfMatch(new SelectionSnapshot(new[] { new CellId(0) }, false));
            var b = BoardHint.OfMatch(new SelectionSnapshot(new[] { new CellId(0) }, false));
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, BoardHint.Draw);
        }

        [Test]
        public void GetHints_LoneKing_ReturnsMatchForThatCell()
        {
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.King)));
            svc.Initialize(FlatLayout(1), new PyramidMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Match, hints[0].Kind);
            Assert.IsTrue(hints[0].Targets.Contains(new CellId(0)));
        }

        [Test]
        public void GetHints_FreePairSumming13_ReturnsMatchWithBothCells()
        {
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Nine), Card(Rank.Four)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Match, hints[0].Kind);
            Assert.IsTrue(hints[0].Targets.Contains(new CellId(0)));
            Assert.IsTrue(hints[0].Targets.Contains(new CellId(1)));
        }

        [Test]
        public void GetHints_WasteTopPlusCellSumming13_ReturnsMatchWithCellAndWaste()
        {
            // cell0 = Four, stock = [Nine]; draw → waste-top = Nine; 4 + 9 = 13.
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Four), Card(Rank.Nine)));
            svc.Initialize(FlatLayout(1), new PyramidMatchRule(), seed: 0);
            svc.DrawFromStock();
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Match, hints[0].Kind);
            Assert.IsTrue(hints[0].Targets.Contains(new CellId(0)));
            Assert.IsTrue(hints[0].Targets.WasteSelected);
        }

        [Test]
        public void GetHints_NoMatchButStock_ReturnsDraw()
        {
            // cells [2,3] (2+3=5, no King), stock = [7] → suggest a draw.
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Two), Card(Rank.Three), Card(Rank.Seven)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Draw, hints[0].Kind);
        }

        [Test]
        public void GetHints_NoMatchEmptyStockRecyclable_ReturnsRecycle()
        {
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Two), Card(Rank.Three), Card(Rank.Seven)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0, maxRecycles: 1);
            svc.DrawFromStock(); // stock now empty, waste = [7]; no pair sums to 13
            Assert.AreEqual(0, svc.CurrentState.Stock.Count);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual(BoardHintKind.Recycle, hints[0].Kind);
        }

        [Test]
        public void GetHints_FullyStuck_ReturnsEmpty()
        {
            // cells [2,3], no stock, no recycle → nothing to do.
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.Two), Card(Rank.Three)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(0, hints.Count);
        }

        [Test]
        public void GetHints_TwoIndependentMatches_ReturnsBoth()
        {
            var svc = new PyramidGameService(new FixedShuffle(Card(Rank.King), Card(Rank.King)));
            svc.Initialize(FlatLayout(2), new PyramidMatchRule(), seed: 0);
            var hints = svc.GetHints(svc.CurrentState);
            Assert.AreEqual(2, hints.Count);
            Assert.IsTrue(hints.All(h => h.Kind == BoardHintKind.Match));
        }
    }
}
