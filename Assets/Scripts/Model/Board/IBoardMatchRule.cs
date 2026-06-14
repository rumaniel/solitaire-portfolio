using System.Collections.Generic;
using Model.Card;

namespace Model.Board
{
    /// <summary>
    /// Game-specific match strategy (injected, mirrors the IDealRule pattern). Given the cards the player
    /// has selected (in tap order), returns whether they form a match, need more, or are invalid.
    /// Not pair-locked: a size-1 selection may already be a Match (e.g. Pyramid King); a future TriPeaks
    /// rule evaluates a single free card against the waste-top.
    /// </summary>
    public interface IBoardMatchRule
    {
        MatchVerdict Evaluate(IReadOnlyList<PlayingCard> selection);
    }
}
