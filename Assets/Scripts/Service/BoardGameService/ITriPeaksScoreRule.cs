namespace Service.BoardGameService
{
    /// <summary>TriPeaks scoring (MSSC). A run is the consecutive cards cleared with no stock draw
    /// between them; the streak counter that feeds <see cref="PointsForStreak"/> lives in the scorer,
    /// not here. These are plain, stateless values (mirrors IBoardScoreRule's role for Pyramid).</summary>
    public interface ITriPeaksScoreRule
    {
        /// <summary>Points for clearing the card at this 1-based position in the current run (default 50×streak).</summary>
        int PointsForStreak(int streak);

        /// <summary>Bonus for clearing the Nth peak tip (1-based ordinal); default 500 / 1000 / 5000.</summary>
        int PeakBonus(int peakOrdinal);

        /// <summary>Penalty per stock draw (default -5); the draw also resets the run.</summary>
        int StockDrawPenalty { get; }
    }
}
