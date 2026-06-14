using System.Collections.Generic;
using Model.Board;
using Model.Card;
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class PyramidMatchRuleTests
    {
        private readonly PyramidMatchRule rule = new PyramidMatchRule();
        private static PlayingCard C(Rank r) => new PlayingCard(r, Suit.Spade);

        [Test]
        public void King_AloneIsMatch()
            => Assert.AreEqual(MatchVerdict.Match, rule.Evaluate(new List<PlayingCard> { C(Rank.King) }));

        [Test]
        public void NonKing_AloneIsIncomplete()
            => Assert.AreEqual(MatchVerdict.Incomplete, rule.Evaluate(new List<PlayingCard> { C(Rank.Five) }));

        [Test]
        public void PairSummingTo13_IsMatch()
            => Assert.AreEqual(MatchVerdict.Match, rule.Evaluate(new List<PlayingCard> { C(Rank.Nine), C(Rank.Four) }));

        [Test]
        public void PairNotSummingTo13_IsInvalid()
            => Assert.AreEqual(MatchVerdict.Invalid, rule.Evaluate(new List<PlayingCard> { C(Rank.Nine), C(Rank.Five) }));

        [Test]
        public void AceQueen_IsMatch() // 1 + 12 = 13
            => Assert.AreEqual(MatchVerdict.Match, rule.Evaluate(new List<PlayingCard> { C(Rank.Ace), C(Rank.Queen) }));
    }
}
