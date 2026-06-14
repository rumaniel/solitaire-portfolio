using Model.Board;
using Model.Card;
using Model.Game;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class BoardRulesTests
    {
        private static PlayingCard Any() => new PlayingCard(Rank.Ace, Suit.Spade);

        [Test]
        public void IsFree_UnknownCellId_ReturnsFalse()
        {
            var cells = new[] { new BoardCell(new CellId(0), null) };
            var layout = new BoardLayout(GameType.None, 1, cells);
            var state = new BoardState(new[] { Any() });
            // CellId not present in this layout → not free, and must not throw.
            Assert.IsFalse(BoardRules.IsFree(layout, state, new CellId(99)));
        }

        [Test]
        public void IsFree_2D_CellIsLockedWhileAnyCoverRemains()
        {
            // cell 0 covered by cells 1 and 2 (pyramid-style).
            var cells = new[]
            {
                new BoardCell(new CellId(0), new[] { new CellId(1), new CellId(2) }),
                new BoardCell(new CellId(1), null),
                new BoardCell(new CellId(2), null),
            };
            var layout = new BoardLayout(GameType.Pyramid, 1, cells);
            var full = new BoardState(new[] { Any(), Any(), Any() });

            Assert.IsFalse(BoardRules.IsFree(layout, full, new CellId(0)), "covered");
            Assert.IsTrue(BoardRules.IsFree(layout, full, new CellId(1)), "bottom is free");

            var oneGone = full.WithCellsRemoved(new[] { new CellId(1) });
            Assert.IsFalse(BoardRules.IsFree(layout, oneGone, new CellId(0)), "still one cover");

            var bothGone = full.WithCellsRemoved(new[] { new CellId(1), new CellId(2) });
            Assert.IsTrue(BoardRules.IsFree(layout, bothGone, new CellId(0)), "uncovered");
        }

        [Test]
        public void IsFree_RemovedCell_IsNeverFree()
        {
            var cells = new[] { new BoardCell(new CellId(0), null) };
            var layout = new BoardLayout(GameType.None, 1, cells);
            var empty = new BoardState(new[] { Any() }).WithCellsRemoved(new[] { new CellId(0) });
            Assert.IsFalse(BoardRules.IsFree(layout, empty, new CellId(0)));
        }
    }
}
