using System;

namespace Model.Skin
{
    /// <summary>Stable identifier for a card skin. Wraps a string for type safety.</summary>
    public readonly struct SkinId : IEquatable<SkinId>
    {
        public string Value { get; }

        public SkinId(string value)
        {
            Value = value;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public bool Equals(SkinId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SkinId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }
}
