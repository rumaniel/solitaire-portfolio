using System.Collections.Generic;
using Model.Board;
using Model.Card;
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class TriPeaksScorerTests
    {
        private static PlayingCard C(Rank r) => new PlayingCard(r, Suit.Spade);

        private static BoardState State(PlayingCard[] cells,
            IReadOnlyList<PlayingCard> stock, IReadOnlyList<PlayingCard> waste)
            => new BoardState(cells, stock, waste);

        private static TriPeaksScorer NewScorer(params int[] apex)
        {
            var set = new List<CellId>();
            foreach (var a in apex) set.Add(new CellId(a));
            return new TriPeaksScorer(new TriPeaksScoreRule(), set);
        }

        [Test]
        public void Play_FirstCard_Scores50_StreakOne()
        {
            var scorer = NewScorer();
            var prev = State(new[] { C(Rank.Eight), C(Rank.Two), C(Rank.Three) },
                stock: null, waste: new[] { C(Rank.Nine) });
            var next = prev.WithCardPlayedToWaste(new CellId(0));
            scorer.Reset(prev);

            var outcome = scorer.Evaluate(prev, next, won: false);
            Assert.AreEqual(BoardScoreEvent.Cleared, outcome.Event);
            Assert.AreEqual(50, outcome.Points);
        }

        [Test]
        public void Play_ConsecutiveCards_EscalateStreak()
        {
            var scorer = NewScorer();
            var s0 = State(new[] { C(Rank.Eight), C(Rank.Seven), C(Rank.Three) },
                stock: null, waste: new[] { C(Rank.Nine) });
            scorer.Reset(s0);

            var s1 = s0.WithCardPlayedToWaste(new CellId(0));
            Assert.AreEqual(50, scorer.Evaluate(s0, s1, false).Points);

            var s2 = s1.WithCardPlayedToWaste(new CellId(1));
            Assert.AreEqual(100, scorer.Evaluate(s1, s2, false).Points);
        }

        [Test]
        public void StockDraw_ResetsStreak_AndScoresMinusFive()
        {
            var scorer = NewScorer();
            var s0 = State(new[] { C(Rank.Eight), C(Rank.Seven), C(Rank.Three) },
                stock: new[] { C(Rank.King) }, waste: new[] { C(Rank.Nine) });
            scorer.Reset(s0);

            var s1 = s0.WithCardPlayedToWaste(new CellId(0));
            scorer.Evaluate(s0, s1, false);

            var s2 = s1.WithStockDrawn();
            var drawOutcome = scorer.Evaluate(s1, s2, false);
            Assert.AreEqual(BoardScoreEvent.Draw, drawOutcome.Event);
            Assert.AreEqual(-5, drawOutcome.Points);

            var s3 = s2.WithCardPlayedToWaste(new CellId(1));
            Assert.AreEqual(50, scorer.Evaluate(s2, s3, false).Points);
        }

        [Test]
        public void Play_ApexCell_AddsPeakBonus_ByOrdinal()
        {
            var scorer = NewScorer(0, 1);
            var s0 = State(new[] { C(Rank.Eight), C(Rank.Eight), C(Rank.Three) },
                stock: null, waste: new[] { C(Rank.Nine) });
            scorer.Reset(s0);

            var s1 = s0.WithCardPlayedToWaste(new CellId(0));
            var o1 = scorer.Evaluate(s0, s1, false);
            Assert.AreEqual(50 + 500, o1.Points);

            var s2 = s1.WithCardPlayedToWaste(new CellId(1));
            var o2 = scorer.Evaluate(s1, s2, false);
            Assert.AreEqual(100 + 1000, o2.Points);
        }

        [Test]
        public void Reset_DerivesPeaksClearedFromAlreadyEmptyApexes()
        {
            var scorer = NewScorer(0, 1, 2);
            var start = State(new[] { (PlayingCard)null, C(Rank.Eight), C(Rank.Five) },
                stock: null, waste: new[] { C(Rank.Nine) });
            scorer.Reset(start); // peaksCleared seeded to 1

            var next = start.WithCardPlayedToWaste(new CellId(1));
            var outcome = scorer.Evaluate(start, next, false);
            Assert.AreEqual(50 + 1000, outcome.Points);
        }

        [Test]
        public void Undo_RevertsStreak_SoTheReplayScoresTheSame()
        {
            var scorer = NewScorer();
            var s0 = State(new[] { C(Rank.Eight), C(Rank.Seven), C(Rank.Three) },
                stock: null, waste: new[] { C(Rank.Nine) });
            scorer.Reset(s0);

            var s1 = s0.WithCardPlayedToWaste(new CellId(0));
            Assert.AreEqual(50, scorer.Evaluate(s0, s1, false).Points);  // streak 1
            var s2 = s1.WithCardPlayedToWaste(new CellId(1));
            Assert.AreEqual(100, scorer.Evaluate(s1, s2, false).Points); // streak 2

            scorer.Undo(); // revert the streak-2 play

            // Replaying the same card must again be streak 2 (= 100), not a runaway streak 3.
            Assert.AreEqual(100, scorer.Evaluate(s1, s2, false).Points);
        }

        [Test]
        public void Undo_RevertsPeakOrdinal_SoTheNextPeakIsNotOverPaid()
        {
            var scorer = NewScorer(0, 1); // cells 0 and 1 are apexes
            var s0 = State(new[] { C(Rank.Eight), C(Rank.Eight), C(Rank.Three) },
                stock: null, waste: new[] { C(Rank.Nine) });
            scorer.Reset(s0);

            var s1 = s0.WithCardPlayedToWaste(new CellId(0)); // apex #1 → 50 + 500
            Assert.AreEqual(50 + 500, scorer.Evaluate(s0, s1, false).Points);

            scorer.Undo(); // revert the apex play

            // The next apex play is again the 1st peak (500), not the 2nd (1000).
            Assert.AreEqual(50 + 500, scorer.Evaluate(s0, s1, false).Points);
        }

        [Test]
        public void Undo_OnFreshScorer_IsNoOp()
        {
            var scorer = NewScorer();
            var s0 = State(new[] { C(Rank.Eight), C(Rank.Two), C(Rank.Three) },
                stock: null, waste: new[] { C(Rank.Nine) });
            scorer.Reset(s0);
            Assert.DoesNotThrow(() => scorer.Undo()); // empty history → no throw
            var s1 = s0.WithCardPlayedToWaste(new CellId(0));
            Assert.AreEqual(50, scorer.Evaluate(s0, s1, false).Points); // streak still starts at 1
        }

        [Test]
        public void Undo_AfterStockDraw_RestoresPreDrawStreak()
        {
            var scorer = NewScorer();
            var s0 = State(new[] { C(Rank.Eight), C(Rank.Seven), C(Rank.Three) },
                stock: new[] { C(Rank.King) }, waste: new[] { C(Rank.Nine) });
            scorer.Reset(s0);

            var s1 = s0.WithCardPlayedToWaste(new CellId(0));
            Assert.AreEqual(50, scorer.Evaluate(s0, s1, false).Points);   // streak → 1

            var s2 = s1.WithStockDrawn();
            Assert.AreEqual(-5, scorer.Evaluate(s1, s2, false).Points);    // draw resets streak → 0

            scorer.Undo(); // revert the draw: streak must restore to its pre-draw value (1), not a fresh-deal 0

            // The next clear therefore continues the streak at 2 (= 100), proving the draw's reset was undone.
            var s3 = s2.WithCardPlayedToWaste(new CellId(1));
            Assert.AreEqual(100, scorer.Evaluate(s2, s3, false).Points);
        }
    }
}
