using System.Collections.Generic;

namespace Model.Board
{
    /// <summary>Pure board predicates for the cover-based games (Pyramid, TriPeaks).</summary>
    public static class BoardRules
    {
        /// <summary>A cell is free iff it still holds a card and every cover-blocker is removed.</summary>
        public static bool IsFree(BoardLayout layout, BoardState state, CellId id)
        {
            // Unknown id (not in this layout) is never free — also guards the HasCard index below.
            if (!layout.TryGetCell(id, out var cell)) return false;
            if (!state.HasCard(id)) return false;

            foreach (var blocker in cell.CoverBlockers)
                if (state.HasCard(blocker)) return false;

            return true;
        }

        public static IEnumerable<CellId> FreeCells(BoardLayout layout, BoardState state)
        {
            foreach (var cell in layout.Cells)
                if (IsFree(layout, state, cell.Id))
                    yield return cell.Id;
        }
    }
}
