using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Model.Card;
using Service.GameService;

namespace Tests.EditMode
{
    /// <summary>
    /// Statistical analysis of shuffle distribution.
    /// Verifies that the Fisher-Yates implementation produces results
    /// consistent with theoretical expectations for a uniform random permutation.
    ///
    /// All tests are marked [Explicit] — they run 10,000 trials each and are intended
    /// for manual diagnostic use, not for CI/automated test runs.
    /// </summary>
    [TestFixture]
    public class ShuffleDistributionTests
    {
        private const int Trials = 10000;

        /// <summary>
        /// In a random 52-card deck with 13 ranks (4 cards each),
        /// the probability that two adjacent cards share the same rank is 3/51.
        /// Over 51 adjacent pairs, the expected count = 51 * 3/51 = 3.0 pairs per deal.
        /// </summary>
        [Test, Explicit("Diagnostic: runs 10k trials")]
        public void SameRankAdjacentPairs_AverageNearTheoretical()
        {
            const double theoreticalExpected = 3.0; // 51 * (3/51)
            int totalPairs = 0;

            for (int t = 0; t < Trials; t++)
            {
                var deck = DeckFactory.CreateShuffled(t);
                totalPairs += CountSameRankAdjacentPairs(deck);
            }

            double average = (double)totalPairs / Trials;
            // std error ≈ sqrt(Var/n) ≈ 0.017 for 10k trials; ±0.15 is very generous
            Assert.That(average, Is.InRange(theoreticalExpected - 0.15, theoreticalExpected + 0.15),
                $"Average same-rank adjacent pairs: {average:F3}, expected ~{theoreticalExpected}");
        }

        /// <summary>
        /// Sanity-checks the shape of the same-rank adjacent pairs distribution.
        /// </summary>
        [Test, Explicit("Diagnostic: runs 10k trials")]
        public void SameRankAdjacentPairs_DistributionSanityCheck()
        {
            var histogram = new Dictionary<int, int>();

            for (int t = 0; t < Trials; t++)
            {
                var deck = DeckFactory.CreateShuffled(t);
                int pairs = CountSameRankAdjacentPairs(deck);
                histogram[pairs] = histogram.GetValueOrDefault(pairs, 0) + 1;
            }

            // 0-pair deals should be < 10% (theoretical ~5%)
            int zeroPairs = histogram.GetValueOrDefault(0, 0);
            Assert.Less((double)zeroPairs / Trials, 0.10, "Too many 0-pair deals");

            // 10+ pair deals should be < 0.5% (extremely rare tail)
            int tenPlus = histogram.Where(kv => kv.Key >= 10).Sum(kv => kv.Value);
            Assert.Less((double)tenPlus / Trials, 0.005, "Too many 10+ pair deals");
        }

        /// <summary>
        /// Verify uniform rank distribution at each deck position.
        /// Each rank should appear at each position with equal probability (4/52 ≈ 7.69%).
        /// </summary>
        [Test, Explicit("Diagnostic: runs 10k trials")]
        public void RankDistributionPerPosition_IsUniform()
        {
            // rankCounts[position, rank] = count
            var rankCounts = new int[52, 14]; // rank 1~13

            for (int t = 0; t < Trials; t++)
            {
                var deck = DeckFactory.CreateShuffled(t);
                for (int pos = 0; pos < 52; pos++)
                    rankCounts[pos, (int)deck[pos].Rank]++;
            }

            double expected = Trials * 4.0 / 52.0; // ~769.23
            int violations = 0;

            // Chi-squared test per position
            for (int pos = 0; pos < 52; pos++)
            {
                double chiSq = 0;
                for (int r = 1; r <= 13; r++)
                {
                    double diff = rankCounts[pos, r] - expected;
                    chiSq += diff * diff / expected;
                }
                // df=12, chi-sq critical at p=0.001 is 32.91
                if (chiSq > 32.91)
                    violations++;
            }

            // At p=0.001, we'd expect 52*0.001 = 0.052 false positives
            // Allow up to 3 violations as conservative bound
            Assert.LessOrEqual(violations, 3,
                $"Too many positions with non-uniform rank distribution: {violations}/52");
        }

        /// <summary>
        /// Loose sanity check: same-rank cards should not be excessively clustered or spread.
        /// For 4 cards of each rank placed uniformly among 52 positions,
        /// the expected average gap between consecutive same-rank cards is (N+1)/(k+1) = 53/5 = 10.6,
        /// where N=52 total cards and k=4 copies per rank.
        /// </summary>
        [Test, Explicit("Diagnostic: runs 10k trials")]
        public void SameRankSpacing_SanityCheck()
        {
            const double expectedAvgGap = 10.6; // (52+1)/(4+1) = 10.6
            double totalAvgGap = 0;

            for (int t = 0; t < Trials; t++)
            {
                var deck = DeckFactory.CreateShuffled(t);
                totalAvgGap += MeasureAverageSameRankGap(deck);
            }

            double overallAvg = totalAvgGap / Trials;
            Assert.That(overallAvg, Is.InRange(expectedAvgGap - 1.5, expectedAvgGap + 1.5),
                $"Average same-rank gap: {overallAvg:F2}, expected ~{expectedAvgGap}");
        }

        // --- Helpers ---

        private static int CountSameRankAdjacentPairs(List<PlayingCard> deck)
        {
            int count = 0;
            for (int i = 1; i < deck.Count; i++)
            {
                if (deck[i].Rank == deck[i - 1].Rank)
                    count++;
            }
            return count;
        }

        private static double MeasureAverageSameRankGap(List<PlayingCard> deck)
        {
            var positions = new Dictionary<Rank, List<int>>();
            for (int i = 0; i < deck.Count; i++)
            {
                var rank = deck[i].Rank;
                if (!positions.ContainsKey(rank))
                    positions[rank] = new List<int>();
                positions[rank].Add(i);
            }

            double totalGap = 0;
            int gapCount = 0;
            foreach (var kv in positions)
            {
                var pos = kv.Value;
                for (int i = 1; i < pos.Count; i++)
                {
                    totalGap += pos[i] - pos[i - 1];
                    gapCount++;
                }
            }

            return gapCount > 0 ? totalGap / gapCount : 0;
        }
    }
}
