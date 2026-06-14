namespace Service.RouteService
{
    /// <summary>Constant keys for RouteService query parameters.</summary>
    public static class GameRouteParams
    {
        /// <summary>Game type enum name (e.g. "Klondike", "Easthaven").</summary>
        public const string GameType = "gameType";

        /// <summary>Table ID selected in the lobby.</summary>
        public const string TableId = "tableId";

        /// <summary>Seed for deterministic deck shuffling.</summary>
        public const string Seed = "seed";

        /// <summary>Game-type-specific variant id (e.g. Klondike drawCount, Spider suitCount).</summary>
        public const string Variant = "variant";

        /// <summary>Legacy Klondike draw count. Kept for backward compatibility; prefer Variant.</summary>
        public const string DrawCount = "drawCount";

        /// <summary>When "true", Ingame resumes the saved snapshot instead of dealing fresh.</summary>
        public const string Continue = "continue";

        /// <summary>
        /// Play mode. Absent/null = normal. "daily" = Daily Challenge (seed derived
        /// from UTC date + GameType; separate snapshot/stats slots).
        /// </summary>
        public const string Mode = "mode";

        /// <summary>Mode value for Daily Challenge.</summary>
        public const string ModeDaily = "daily";
    }
}
