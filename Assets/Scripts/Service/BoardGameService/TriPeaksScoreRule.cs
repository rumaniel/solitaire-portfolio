namespace Service.BoardGameService
{
    /// <summary>Microsoft-Solitaire-Collection TriPeaks scoring with tunable constants.</summary>
    public sealed class TriPeaksScoreRule : ITriPeaksScoreRule
    {
        private readonly int pointsPerStreakStep;
        private readonly int[] peakBonuses;

        public TriPeaksScoreRule(int pointsPerStreakStep = 50, int stockDrawPenalty = -5,
            int firstPeakBonus = 500, int secondPeakBonus = 1000, int thirdPeakBonus = 5000)
        {
            this.pointsPerStreakStep = pointsPerStreakStep;
            StockDrawPenalty = stockDrawPenalty;
            peakBonuses = new[] { firstPeakBonus, secondPeakBonus, thirdPeakBonus };
        }

        public int PointsForStreak(int streak) => streak <= 0 ? 0 : (pointsPerStreakStep * streak);

        public int PeakBonus(int peakOrdinal)
            => ((peakOrdinal >= 1) && (peakOrdinal <= peakBonuses.Length)) ? peakBonuses[peakOrdinal - 1] : 0;

        public int StockDrawPenalty { get; }
    }
}
