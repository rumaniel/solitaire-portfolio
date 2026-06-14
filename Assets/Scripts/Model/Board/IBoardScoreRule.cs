namespace Model.Board
{
    /// <summary>
    /// Scoring for a cover-match board game. Plain values; the presenter calls these to feed SessionStats
    /// (mirrors how IScoreRule drives card scoring, but board events differ — no pile types).
    /// </summary>
    public interface IBoardScoreRule
    {
        /// <summary>Points awarded for clearing <paramref name="cardCount"/> cards in one match (pair = 2, King = 1).</summary>
        int ScoreForRemoval(int cardCount);

        /// <summary>Bonus added when the board is fully cleared.</summary>
        int BoardClearedBonus { get; }

        /// <summary>Points for a stock draw — a per-draw efficiency penalty (&lt;= 0). Fewer draws score higher.</summary>
        int ScoreForStockDraw { get; }

        /// <summary>Points for a waste→stock recycle — a larger efficiency penalty (&lt;= 0) than a single draw.</summary>
        int ScoreForRecycle { get; }
    }
}
