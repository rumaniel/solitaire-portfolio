using System;
using System.Collections.Generic;

namespace Model.Board
{
    /// <summary>
    /// Immutable logical topology of one board position: the cover graph that drives the free-cell
    /// predicate (a card is free once nothing covers it). Render position is a View concern — fixed
    /// prefab anchors for Pyramid/TriPeaks — and is intentionally NOT stored here. No UnityEngine types.
    /// </summary>
    public sealed class BoardCell : IEquatable<BoardCell>
    {
        public CellId Id { get; }

        /// <summary>Cells that must ALL be removed before this cell is free (covering it from above/front).</summary>
        public IReadOnlyList<CellId> CoverBlockers { get; }

        private static readonly IReadOnlyList<CellId> EmptyBlockers = new List<CellId>(0).AsReadOnly();

        public BoardCell(CellId id, IReadOnlyList<CellId> coverBlockers)
        {
            Id = id;
            // Defensive read-only copy so external code cannot mutate cover topology after construction.
            CoverBlockers = coverBlockers == null || coverBlockers.Count == 0
                ? EmptyBlockers
                : new List<CellId>(coverBlockers).AsReadOnly();
        }

        public bool Equals(BoardCell other) => other != null && Id.Equals(other.Id);
        public override bool Equals(object obj) => obj is BoardCell other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
    }
}
