using Model.Board;
using Model.Card;
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class TriPeaksMatchRuleTests
    {
        private static readonly TriPeaksMatchRule Rule = new TriPeaksMatchRule();
        private static PlayingCard Card(Rank r) => new PlayingCard(r, Suit.Spade);
        private static MatchVerdict Eval(Rank a, Rank b)
            => Rule.Evaluate(new[] { Card(a), Card(b) });

        [Test] public void OneApart_IsMatch() => Assert.AreEqual(MatchVerdict.Match, Eval(Rank.Seven, Rank.Eight));
        [Test] public void OneApart_Descending_IsMatch() => Assert.AreEqual(MatchVerdict.Match, Eval(Rank.Eight, Rank.Seven));
        [Test] public void AceOnTwo_IsMatch() => Assert.AreEqual(MatchVerdict.Match, Eval(Rank.Two, Rank.Ace));
        [Test] public void KingOnAce_WrapsToMatch() => Assert.AreEqual(MatchVerdict.Match, Eval(Rank.Ace, Rank.King));
        [Test] public void AceOnKing_WrapsToMatch() => Assert.AreEqual(MatchVerdict.Match, Eval(Rank.King, Rank.Ace));
        [Test] public void SameRank_IsInvalid() => Assert.AreEqual(MatchVerdict.Invalid, Eval(Rank.Five, Rank.Five));
        [Test] public void TwoApart_IsInvalid() => Assert.AreEqual(MatchVerdict.Invalid, Eval(Rank.Five, Rank.Seven));
        [Test] public void KingAndQueen_IsMatch() => Assert.AreEqual(MatchVerdict.Match, Eval(Rank.King, Rank.Queen));

        [Test]
        public void SingleCard_IsIncomplete()
        {
            Assert.AreEqual(MatchVerdict.Incomplete, Rule.Evaluate(new[] { Card(Rank.Five) }));
        }
    }
}
