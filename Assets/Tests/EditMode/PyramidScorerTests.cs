using System.Collections.Generic;
using Model.Board;
using Model.Card;
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class PyramidScorerTests
    {
        private static PlayingCard C(Rank r) => new PlayingCard(r, Suit.Spade);

        private static BoardState State(PlayingCard c0, PlayingCard c1,
            IReadOnlyList<PlayingCard> stock, IReadOnlyList<PlayingCard> waste, int recycle = 0)
            => new BoardState(new[] { c0, c1 }, stock, waste, recycle);

        private static PyramidScorer NewScorer() => new PyramidScorer(new PyramidScoreRule());

        [Test]
        public void Removal_ScoresPerCard_AndEventIsCleared()
        {
            var scorer = NewScorer();
            var prev = State(C(Rank.Nine), C(Rank.Four), stock: null, waste: null);
            var next = prev.WithCellsRemoved(new[] { new CellId(0), new CellId(1) });
            scorer.Reset(prev);

            var outcome = scorer.Evaluate(prev, next, won: false);
            Assert.AreEqual(BoardScoreEvent.Cleared, outcome.Event);
            Assert.AreEqual(10, outcome.Points); // perCard 5 × 2
        }

        [Test]
        public void Removal_OnWin_AddsBoardClearedBonus()
        {
            var scorer = NewScorer();
            var prev = State(C(Rank.King), C(Rank.King), stock: null, waste: null);
            var afterFirst = prev.WithCellsRemoved(new[] { new CellId(0) });
            var next = afterFirst.WithCellsRemoved(new[] { new CellId(1) });
            scorer.Reset(afterFirst);

            var outcome = scorer.Evaluate(afterFirst, next, won: true);
            Assert.AreEqual(BoardScoreEvent.Cleared, outcome.Event);
            Assert.AreEqual(5 + 100, outcome.Points); // 1 card × 5 + boardClearedBonus 100
        }

        [Test]
        public void StockDraw_ScoresDrawPenalty_AndEventIsDraw()
        {
            var scorer = NewScorer();
            var prev = State(C(Rank.Nine), C(Rank.Four), stock: new[] { C(Rank.Seven) }, waste: null);
            var next = prev.WithStockDrawn();
            scorer.Reset(prev);

            var outcome = scorer.Evaluate(prev, next, won: false);
            Assert.AreEqual(BoardScoreEvent.Draw, outcome.Event);
            Assert.AreEqual(-2, outcome.Points);
        }

        [Test]
        public void Recycle_ScoresRecyclePenalty_AndEventIsRecycle()
        {
            var scorer = NewScorer();
            var prev = State(C(Rank.Nine), C(Rank.Four), stock: null, waste: new[] { C(Rank.Seven) });
            var next = prev.WithStockRecycled();
            scorer.Reset(prev);

            var outcome = scorer.Evaluate(prev, next, won: false);
            Assert.AreEqual(BoardScoreEvent.Recycle, outcome.Event);
            Assert.AreEqual(-10, outcome.Points);
        }
    }
}
