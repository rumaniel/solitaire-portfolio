using System;

namespace Model.Board
{
    /// <summary>Stable identifier of a position within a <see cref="BoardLayout"/>. Value is a dense 0-based index.</summary>
    public readonly struct CellId : IEquatable<CellId>
    {
        public int Value { get; }
        public CellId(int value) { Value = value; }

        public bool Equals(CellId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CellId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Cell({Value})";
    }
}
