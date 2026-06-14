using System;
using System.Collections.Generic;

namespace Model.Board
{
    /// <summary>Immutable view of the board's pending tap-selection: chosen cells plus whether the waste-top is chosen.</summary>
    public sealed class SelectionSnapshot : IEquatable<SelectionSnapshot>
    {
        public static readonly SelectionSnapshot Empty = new SelectionSnapshot(Array.Empty<CellId>(), false);

        private readonly CellId[] cells;
        public IReadOnlyList<CellId> Cells => cells;
        public bool WasteSelected { get; }

        public SelectionSnapshot(IReadOnlyList<CellId> cells, bool wasteSelected)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            this.cells = new CellId[cells.Count];
            for (int i = 0; i < cells.Count; i++) this.cells[i] = cells[i];
            WasteSelected = wasteSelected;
        }

        public bool Contains(CellId id)
        {
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].Equals(id)) return true;
            return false;
        }

        public bool Equals(SelectionSnapshot other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (WasteSelected != other.WasteSelected) return false;
            if (cells.Length != other.cells.Length) return false;
            for (int i = 0; i < cells.Length; i++)
                if (!cells[i].Equals(other.cells[i])) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is SelectionSnapshot o && Equals(o);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var c in cells) hash.Add(c);
            hash.Add(WasteSelected);
            return hash.ToHashCode();
        }
    }
}
