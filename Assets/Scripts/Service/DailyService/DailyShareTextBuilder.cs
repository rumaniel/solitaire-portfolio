using System;
using System.Globalization;
using Model.Stats;
using Service.GameService;
using Shared;

namespace Service.DailyService
{
    /// <summary>Token substitution for daily share text: {date} {time} {score} {moves} {streak} {url}.</summary>
    public static class DailyShareTextBuilder
    {
        public static string Build(
            string template,
            string playUrl,
            DateTime utcDate,
            SessionStats session,
            int streak)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            session ??= new SessionStats();
            return template
                .Replace("{date}", DailySeed.DateKey(utcDate))
                .Replace("{time}", TimeFormatHelper.Format(session.ElapsedSeconds))
                .Replace("{score}", session.Score.ToString(CultureInfo.InvariantCulture))
                .Replace("{moves}", session.MoveCount.ToString(CultureInfo.InvariantCulture))
                .Replace("{streak}", streak.ToString(CultureInfo.InvariantCulture))
                .Replace("{url}", playUrl ?? string.Empty);
        }

        public static string Build(
            string template,
            string playUrl,
            DateTime utcDate,
            DailyRecord record,
            int streak)
        {
            if (string.IsNullOrEmpty(template) || record == null) return string.Empty;
            return template
                .Replace("{date}", DailySeed.DateKey(utcDate))
                .Replace("{time}", TimeFormatHelper.Format(record.ElapsedSeconds))
                .Replace("{score}", record.Score.ToString(CultureInfo.InvariantCulture))
                .Replace("{moves}", record.MoveCount.ToString(CultureInfo.InvariantCulture))
                .Replace("{streak}", streak.ToString(CultureInfo.InvariantCulture))
                .Replace("{url}", playUrl ?? string.Empty);
        }
    }
}
