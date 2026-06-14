using System;
using System.Globalization;
using Model.Game;

namespace Gateway.Snapshot
{
    /// <summary>
    /// Composite identifier (GameType + VariantId [+ Mode]) for a single saved
    /// game variant. <c>Mode</c> is optional and defaults to null for normal play
    /// — preserves existing snapshot filenames for backward compatibility.
    /// </summary>
    public readonly struct SnapshotKey : IEquatable<SnapshotKey>
    {
        public readonly GameType GameType;
        public readonly int VariantId;
        public readonly string Mode;

        public SnapshotKey(GameType gameType, int variantId, string mode = null)
        {
            GameType = gameType;
            VariantId = variantId;
            Mode = string.IsNullOrEmpty(mode) ? null : mode;
        }

        public bool Equals(SnapshotKey other)
            => GameType == other.GameType
               && VariantId == other.VariantId
               && string.Equals(Mode, other.Mode, StringComparison.Ordinal);

        public override bool Equals(object obj)
            => obj is SnapshotKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ((int)GameType * 397) ^ VariantId;
                if (Mode != null)
                    hash = (hash * 397) ^ Mode.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Format: <c>{GameType}_Draw{VariantId}</c> for normal, or
        /// <c>{GameType}_Draw{VariantId}_{mode}</c> when a mode is present.
        /// "Draw" infix kept for backward compatibility with existing files.
        /// </summary>
        public override string ToString()
        {
            var baseKey = string.Concat(
                GameType.ToString(),
                "_Draw",
                VariantId.ToString(CultureInfo.InvariantCulture));
            return Mode == null ? baseKey : string.Concat(baseKey, "_", Mode);
        }

        public static bool operator ==(SnapshotKey left, SnapshotKey right) => left.Equals(right);
        public static bool operator !=(SnapshotKey left, SnapshotKey right) => !left.Equals(right);
    }
}
