using System;
using System.Globalization;
using Model.Game;

namespace Service.GameService
{
    /// <summary>
    /// Deterministic seed and date-key helpers for the Daily Challenge.
    /// Same (utcDate, gameType) always yields the same seed across all clients.
    /// </summary>
    public static class DailySeed
    {
        /// <summary>
        /// Mix (year, month, day, gameType) into a single int via the common
        /// 17/31 hash. <c>unchecked</c> allows wrapping for far-future years.
        /// </summary>
        public static int For(DateTime utcDate, GameType gameType)
        {
            var d = utcDate.Date;
            unchecked
            {
                int seed = 17;
                seed = seed * 31 + d.Year;
                seed = seed * 31 + d.Month;
                seed = seed * 31 + d.Day;
                seed = seed * 31 + (int)gameType;
                return seed;
            }
        }

        /// <summary>Invariant "yyyy-MM-dd" key for persisting per-day state.</summary>
        public static string DateKey(DateTime utcDate)
            => utcDate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
