using System;

namespace Shared
{
    /// <summary>
    /// Elapsed-time string formatting used across scenes (HUD, panels, Lobby
    /// tile, share text). Lives in Shared so Lobby views don't have to
    /// reach into Scene.Ingame for a pure formatter.
    /// </summary>
    public static class TimeFormatHelper
    {
        /// <summary>
        /// Formats elapsed seconds as "h:mm:ss" when >= 1 hour, otherwise "m:ss".
        /// </summary>
        public static string Format(float seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1
                ? ts.ToString(@"h\:mm\:ss")
                : ts.ToString(@"m\:ss");
        }
    }
}
