using System.Collections.Generic;
using Model.Board;
using Model.Game;

namespace Service.BoardGameService
{
    /// <summary>
    /// Builds the classic three-peak, 28-card TriPeaks topology in code. Logical cover graph only;
    /// render positions are fixed prefab anchors supplied by the View (the three visually separate
    /// peaks are a render concern — the cover graph is a single dense set, with rows 2↔3 forming one
    /// continuous base strip that links the peaks).
    /// Cell ids: row0 (apexes) 0..2, row1 3..8, row2 9..17, row3 (base) 18..27.
    /// </summary>
    public static class TriPeaksLayoutFactory
    {
        public const int CellCount = 28; // 3 + 6 + 9 + 10

        public static readonly IReadOnlyList<CellId> ApexCellIds =
            System.Array.AsReadOnly(new[] { new CellId(0), new CellId(1), new CellId(2) });

        public static BoardLayout Create(int variant = 1)
        {
            var cells = new List<BoardCell>(CellCount);

            // Row 0 (apex p, id p): covered by the two row-1 cells of peak p.
            for (int p = 0; p < 3; p++)
                cells.Add(new BoardCell(new CellId(p), new[] { new CellId(3 + (2 * p)), new CellId(4 + (2 * p)) }));

            // Row 1 (ids 3..8): peak p's left cell (3+2p) covered by row-2 (9+3p),(10+3p);
            // its right cell (4+2p) covered by (10+3p),(11+3p).
            for (int p = 0; p < 3; p++)
            {
                cells.Add(new BoardCell(new CellId(3 + (2 * p)),
                    new[] { new CellId(9 + (3 * p)), new CellId(10 + (3 * p)) }));
                cells.Add(new BoardCell(new CellId(4 + (2 * p)),
                    new[] { new CellId(10 + (3 * p)), new CellId(11 + (3 * p)) }));
            }

            // Row 2 (ids 9..17): cell 9+j covered by base cells 18+j and 19+j (continuous strip).
            for (int j = 0; j < 9; j++)
                cells.Add(new BoardCell(new CellId(9 + j), new[] { new CellId(18 + j), new CellId(19 + j) }));

            // Row 3 / base (ids 18..27): fully exposed, no cover.
            for (int b = 18; b < 28; b++)
                cells.Add(new BoardCell(new CellId(b), null));

            return new BoardLayout(GameType.TriPeaks, variant, cells);
        }
    }
}
