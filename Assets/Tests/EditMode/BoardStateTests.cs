using System;
using System.Collections.Generic;
using Model.Board;
using Model.Card;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class BoardStateTests
    {
        [Test]
        public void CellId_EqualityAndHash_ByValue()
        {
            Assert.AreEqual(new CellId(5), new CellId(5));
            Assert.AreNotEqual(new CellId(5), new CellId(6));
            Assert.AreEqual(new CellId(5).GetHashCode(), new CellId(5).GetHashCode());
        }

        [Test]
        public void BoardCell_StoresTopology_AndEqualsById()
        {
            var cell = new BoardCell(new CellId(3), new[] { new CellId(7), new CellId(8) });

            Assert.AreEqual(2, cell.CoverBlockers.Count);
            Assert.AreEqual(new CellId(7), cell.CoverBlockers[0]);
            Assert.AreEqual(new CellId(8), cell.CoverBlockers[1]);

            var same = new BoardCell(new CellId(3), null);
            Assert.AreEqual(cell, same); // identity by Id
        }

        [Test]
        public void BoardLayout_LooksUpCellsById()
        {
            var cells = new[]
            {
                new BoardCell(new CellId(0), null),
                new BoardCell(new CellId(1), null),
            };
            var layout = new BoardLayout(Model.Game.GameType.Pyramid, 1, cells);

            Assert.AreEqual(2, layout.Count);
            Assert.AreEqual(new CellId(1), layout.Cell(new CellId(1)).Id);
            Assert.IsTrue(layout.TryGetCell(new CellId(0), out _));
            Assert.IsFalse(layout.TryGetCell(new CellId(9), out _));
        }

        private static PlayingCard C(Rank r, Suit s) => new PlayingCard(r, s);

        [Test]
        public void BoardState_RemovesCellsAndDrawsStock_Immutably()
        {
            var cellCards = new[]
            {
                C(Rank.Ace, Suit.Spade),   // CellId 0
                C(Rank.King, Suit.Heart),  // CellId 1
            };
            var stock = new[] { C(Rank.Five, Suit.Club) };
            var state = new BoardState(cellCards, stock, waste: null);

            Assert.IsTrue(state.HasCard(new CellId(0)));
            Assert.AreEqual(Rank.King, state.CardAt(new CellId(1)).Rank);

            var afterRemove = state.WithCellsRemoved(new[] { new CellId(0) });
            Assert.IsFalse(afterRemove.HasCard(new CellId(0)));
            Assert.IsTrue(state.HasCard(new CellId(0)), "original unchanged");

            var afterDraw = state.WithStockDrawn();
            Assert.AreEqual(0, afterDraw.Stock.Count);
            Assert.AreEqual(Rank.Five, afterDraw.WasteTop.Rank);

            var afterWaste = afterDraw.WithWasteTopRemoved();
            Assert.IsNull(afterWaste.WasteTop);
        }

        [Test]
        public void Constructor_DefensivelyCopiesStock_SourceMutationDoesNotLeak()
        {
            var stock = new List<PlayingCard> { C(Rank.Five, Suit.Club) };
            var state = new BoardState(new[] { C(Rank.Ace, Suit.Spade) }, stock, waste: null);

            stock.Add(C(Rank.King, Suit.Heart)); // mutate the source list AFTER construction
            Assert.AreEqual(1, state.Stock.Count, "Stock must not reflect post-construction source mutation");
        }

        [Test]
        public void BoardCell_DefensivelyCopiesCoverBlockers()
        {
            var blockers = new List<CellId> { new CellId(7) };
            var cell = new BoardCell(new CellId(3), blockers);

            blockers.Add(new CellId(8)); // mutate the source list AFTER construction
            Assert.AreEqual(1, cell.CoverBlockers.Count);
        }

        [Test]
        public void BoardLayout_ThrowsOnDuplicateCellId()
        {
            var cells = new[]
            {
                new BoardCell(new CellId(0), null),
                new BoardCell(new CellId(0), null), // duplicate id
            };
            Assert.Throws<ArgumentException>(() => new BoardLayout(Model.Game.GameType.Pyramid, 1, cells));
        }

        [Test]
        public void BoardLayout_ThrowsOnNullCells()
        {
            Assert.Throws<ArgumentNullException>(
                () => new BoardLayout(Model.Game.GameType.Pyramid, 1, null));
        }

        [Test]
        public void BoardLayout_ThrowsOnNonDenseCellId()
        {
            // 2 cells but ids {0, 5} — BoardState indexes by CellId.Value, so id 5 would crash.
            var cells = new[]
            {
                new BoardCell(new CellId(0), null),
                new BoardCell(new CellId(5), null),
            };
            Assert.Throws<ArgumentException>(() => new BoardLayout(Model.Game.GameType.Pyramid, 1, cells));
        }

        [Test]
        public void BoardLayout_ThrowsOnNegativeCellId()
        {
            var cells = new[] { new BoardCell(new CellId(-1), null) };
            Assert.Throws<ArgumentException>(() => new BoardLayout(Model.Game.GameType.Pyramid, 1, cells));
        }

        [Test]
        public void BoardLayout_ThrowsOnCoverBlockerNotInLayout()
        {
            // cell 0 references a cover-blocker (cell 9) that does not exist.
            var cells = new[] { new BoardCell(new CellId(0), new[] { new CellId(9) }) };
            Assert.Throws<ArgumentException>(() => new BoardLayout(Model.Game.GameType.Pyramid, 1, cells));
        }

        [Test]
        public void BoardLayout_AcceptsValidDenseLayout()
        {
            var cells = new[]
            {
                new BoardCell(new CellId(0), new[] { new CellId(1) }), // covered by cell 1
                new BoardCell(new CellId(1), null),
            };
            Assert.DoesNotThrow(() => new BoardLayout(Model.Game.GameType.Pyramid, 1, cells));
        }

        [Test]
        public void WithCardPlayedToWaste_RemovesCell_AndPushesCardToWasteTop()
        {
            var cellCards = new[]
            {
                C(Rank.Seven, Suit.Spade),  // CellId 0
                C(Rank.Nine, Suit.Heart),   // CellId 1
            };
            var waste = new[] { C(Rank.Eight, Suit.Club) };
            var state = new BoardState(cellCards, stock: null, waste: waste);

            var next = state.WithCardPlayedToWaste(new CellId(0));

            Assert.IsFalse(next.HasCard(new CellId(0)), "played cell cleared");
            Assert.IsTrue(next.HasCard(new CellId(1)), "other cell untouched");
            Assert.AreEqual(Rank.Seven, next.WasteTop.Rank, "played card is the new waste-top");
            Assert.AreEqual(2, next.Waste.Count);
            Assert.IsTrue(state.HasCard(new CellId(0)), "original state unchanged (immutability)");
            Assert.AreEqual(Rank.Eight, state.WasteTop.Rank, "original waste-top unchanged");
        }

        [Test]
        public void WithCardPlayedToWaste_EmptyCell_IsNoOp()
        {
            var state = new BoardState(new[] { C(Rank.Seven, Suit.Spade) });
            var cleared = state.WithCellsRemoved(new[] { new CellId(0) });
            var next = cleared.WithCardPlayedToWaste(new CellId(0)); // already empty
            Assert.AreSame(cleared, next);
        }
    }
}
