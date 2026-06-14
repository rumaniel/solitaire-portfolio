using System;

namespace Model.Board
{
    /// <summary>Immutable hint. For <see cref="BoardHintKind.Match"/>, <see cref="Targets"/> holds the
    /// cells (one King, or a pair summing 13) plus the waste flag to highlight. For Draw/Recycle the
    /// stock pile is the affordance, so <see cref="Targets"/> is <see cref="SelectionSnapshot.Empty"/>.</summary>
    public sealed class BoardHint : IEquatable<BoardHint>
    {
        public BoardHintKind Kind { get; }
        public SelectionSnapshot Targets { get; }

        public BoardHint(BoardHintKind kind, SelectionSnapshot targets)
        {
            Kind = kind;
            Targets = targets ?? throw new ArgumentNullException(nameof(targets));
        }

        public static BoardHint OfMatch(SelectionSnapshot targets) => new BoardHint(BoardHintKind.Match, targets);
        public static readonly BoardHint Draw = new BoardHint(BoardHintKind.Draw, SelectionSnapshot.Empty);
        public static readonly BoardHint Recycle = new BoardHint(BoardHintKind.Recycle, SelectionSnapshot.Empty);

        public bool Equals(BoardHint other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Kind == other.Kind && Targets.Equals(other.Targets);
        }

        public override bool Equals(object obj) => obj is BoardHint o && Equals(o);
        public override int GetHashCode() => HashCode.Combine((int)Kind, Targets);
    }
}
