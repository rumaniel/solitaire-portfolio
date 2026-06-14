using System;
using System.Collections.Generic;
using System.Globalization;
using Model.Game;

namespace Service.GameService
{
    /// <summary>
    /// Encodes and decodes a (GameType, seed) pair into a short shareable code.
    /// <br/>Format: <c>{PREFIX}-{HEX_SEED}</c>
    /// <br/>Example: <c>KLO-A3F7B2C1</c>, <c>EAS-00FF1234</c>
    /// <br/>
    /// <br/>Prefixes are auto-generated from the first 3 uppercase characters of each
    /// <see cref="GameType"/> enum name (excluding <c>None</c>).
    /// Adding a new GameType enum value automatically registers its prefix.
    /// </summary>
    public static class GameCode
    {
        private static readonly Dictionary<GameType, string> TypeToPrefix = new();
        private static readonly Dictionary<string, GameType> PrefixToType =
            new(StringComparer.OrdinalIgnoreCase);

        static GameCode()
        {
            foreach (GameType gt in Enum.GetValues(typeof(GameType)))
            {
                if (gt == GameType.None) continue;
                var name = gt.ToString();
                var prefix = name.Length >= 3
                    ? name.Substring(0, 3).ToUpperInvariant()
                    : name.ToUpperInvariant();

                if (PrefixToType.ContainsKey(prefix))
                    throw new InvalidOperationException(
                        $"GameCode prefix collision: '{prefix}' maps to both " +
                        $"{PrefixToType[prefix]} and {gt}. Use distinct first-3-letter names " +
                        $"or add an explicit mapping.");

                TypeToPrefix[gt] = prefix;
                PrefixToType[prefix] = gt;
            }
        }

        /// <summary>
        /// Encodes a game type and seed into a shareable code string.
        /// </summary>
        /// <returns>Code in format "KLO-A3F7B2C1" (12 chars).</returns>
        public static string Encode(GameType gameType, int seed)
        {
            if (!TypeToPrefix.TryGetValue(gameType, out var prefix))
                throw new ArgumentException($"Unsupported game type: {gameType}", nameof(gameType));

            // Convert to unsigned hex to handle negative seeds correctly
            var hex = ((uint)seed).ToString("X8");
            return $"{prefix}-{hex}";
        }

        /// <summary>
        /// Decodes a shareable code string back into a game type and seed.
        /// </summary>
        /// <returns>Decoded (GameType, seed) tuple, or null if the code is invalid.</returns>
        public static (GameType gameType, int seed)? Decode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            code = code.Trim();
            var dashIndex = code.IndexOf('-');
            if (dashIndex < 1 || dashIndex >= code.Length - 1)
                return null;

            var prefix = code.Substring(0, dashIndex);
            var hexPart = code.Substring(dashIndex + 1);

            if (!PrefixToType.TryGetValue(prefix, out var gameType))
                return null;

            // Strict format: exactly 8 hex characters (matches Encode output)
            if (hexPart.Length != 8)
                return null;

            if (!uint.TryParse(hexPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var unsigned))
                return null;

            return (gameType, (int)unsigned);
        }
    }
}
