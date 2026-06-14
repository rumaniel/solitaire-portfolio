using System.Collections.Generic;
using Model.Board;
using Model.Card;

namespace Service.BoardGameService
{
    /// <summary>Pyramid: remove two cards whose ranks sum to 13 (Ace=1..King=13), or a King alone.</summary>
    public sealed class PyramidMatchRule : IBoardMatchRule
    {
        public MatchVerdict Evaluate(IReadOnlyList<PlayingCard> selection)
        {
            if (selection.Count == 1)
                return selection[0].Rank == Rank.King ? MatchVerdict.Match : MatchVerdict.Incomplete;

            if (selection.Count == 2)
            {
                int sum = (int)selection[0].Rank + (int)selection[1].Rank;
                return sum == 13 ? MatchVerdict.Match : MatchVerdict.Invalid;
            }

            return MatchVerdict.Invalid;
        }
    }
}
