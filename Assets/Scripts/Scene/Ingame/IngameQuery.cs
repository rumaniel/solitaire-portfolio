using System.Collections.Generic;
using Model.Game;
using Service.RouteService;

namespace Scene.Ingame
{
    /// <summary>Strongly-typed wrapper around RouteService.CurrentQuery for Ingame scene entry parameters.</summary>
    public class IngameQuery
    {
        /// <summary>
        /// The game type to play.
        /// Falls back to <see cref="GameType.Klondike"/> if the parameter is missing or cannot be parsed.
        /// </summary>
        public GameType GameType { get; }

        /// <summary>
        /// Optional seed for deterministic deck shuffling.
        /// When null, a random seed is generated. When set, reproduces a specific deal.
        /// </summary>
        public int? Seed { get; }

        /// <summary>
        /// Game-type-specific variant id (drawCount, suitCount, etc.). Null when missing or invalid.
        /// Falls back to legacy DrawCount key. Playability validated by DealRuleFactory.
        /// </summary>
        public int? Variant { get; }

        /// <summary>
        /// Whether the caller wants to resume an existing snapshot for this
        /// (GameType, Variant) pair. <c>false</c> means start a fresh game even
        /// if a snapshot exists.
        /// </summary>
        public bool IsContinue { get; }

        /// <summary>
        /// Play mode. Null for normal play. <c>"daily"</c> for Daily Challenge —
        /// seed is derived deterministically from UTC date + GameType.
        /// </summary>
        public string Mode { get; }

        /// <summary>True when <see cref="Mode"/> equals <see cref="GameRouteParams.ModeDaily"/>.</summary>
        public bool IsDaily => string.Equals(Mode, GameRouteParams.ModeDaily, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Creates an IngameQuery from RouteService.CurrentQuery.
        /// </summary>
        /// <param name="query">RouteService.CurrentQuery. If null, all values use defaults.</param>
        public IngameQuery(IReadOnlyDictionary<string, string> query)
        {
            if (query != null
                && query.TryGetValue(GameRouteParams.GameType, out var raw)
                && System.Enum.TryParse<GameType>(raw, ignoreCase: true, out var parsed))
            {
                GameType = parsed;
            }
            else
            {
                GameType = Model.Game.GameType.Klondike;
            }

            if (query != null
                && query.TryGetValue(GameRouteParams.Seed, out var seedRaw)
                && int.TryParse(seedRaw, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsedSeed))
            {
                Seed = parsedSeed;
            }

            Variant = ParseVariant(query);

            IsContinue = query != null
                && query.TryGetValue(GameRouteParams.Continue, out var continueRaw)
                && string.Equals(continueRaw, "true", System.StringComparison.OrdinalIgnoreCase);

            Mode = query != null && query.TryGetValue(GameRouteParams.Mode, out var modeRaw)
                ? modeRaw
                : null;
        }

        private static int? ParseVariant(IReadOnlyDictionary<string, string> query)
        {
            if (query == null) return null;

            if (TryParsePositiveInt(query, GameRouteParams.Variant, out var variant))
                return variant;
            if (TryParsePositiveInt(query, GameRouteParams.DrawCount, out var legacy))
                return legacy;
            return null;
        }

        private static bool TryParsePositiveInt(IReadOnlyDictionary<string, string> query, string key, out int value)
        {
            value = 0;
            if (!query.TryGetValue(key, out var raw)) return false;
            if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out value))
                return false;
            // Variant ids are opaque per GameType but must always be strictly positive.
            if (value < 1)
            {
                value = 0;
                return false;
            }
            return true;
        }
    }
}
