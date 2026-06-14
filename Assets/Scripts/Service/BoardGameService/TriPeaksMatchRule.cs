using System;
using System.Collections.Generic;
using Model.Board;
using Model.Card;

namespace Service.BoardGameService
{
    /// <summary>TriPeaks: a tapped free card plays onto the waste-top when their ranks differ by one,
    /// with Ace&#x2194;King wrap (Ace plays on King and King plays on Ace). The service evaluates the ordered
    /// pair [wasteTop, tapped]; order does not matter to the verdict.</summary>
    public sealed class TriPeaksMatchRule : IBoardMatchRule
    {
        public MatchVerdict Evaluate(IReadOnlyList<PlayingCard> selection)
        {
            if (selection.Count != 2) return MatchVerdict.Incomplete;
            int a = (int)selection[0].Rank;
            int b = (int)selection[1].Rank;
            int diff = Math.Abs(a - b);
            // diff 1 = adjacent ranks; diff 12 = Ace(1)&#x2194;King(13) wrap.
            return ((diff == 1) || (diff == 12)) ? MatchVerdict.Match : MatchVerdict.Invalid;
        }
    }
}
