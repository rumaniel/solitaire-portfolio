using System.Collections.Generic;
using Model.Board;
using Model.Game;

namespace Service.BoardGameService
{
    /// <summary>
    /// Generates the classic 28-card, 7-row Pyramid logical topology in code. Cell ids are assigned
    /// row-by-row (apex = 0). Cell (r,k) is covered by the two cards diagonally below it: (r+1,k) and
    /// (r+1,k+1). Render positions are supplied by the View (fixed prefab anchors), not by this factory.
    /// </summary>
    public static class PyramidLayoutFactory
    {
        public const int Rows = 7;
        public const int CellCount = 28; // 1+2+...+7

        public static BoardLayout Create(int variant = 1)
        {
            var cells = new List<BoardCell>(CellCount);

            for (int r = 0; r < Rows; r++)
            {
                int rowStart = r * (r + 1) / 2;
                for (int k = 0; k <= r; k++)
                {
                    int index = rowStart + k;

                    var covers = new List<CellId>(2);
                    if (r < Rows - 1)
                    {
                        int belowStart = (r + 1) * (r + 2) / 2;
                        covers.Add(new CellId(belowStart + k));
                        covers.Add(new CellId(belowStart + k + 1));
                    }

                    cells.Add(new BoardCell(new CellId(index), covers));
                }
            }

            return new BoardLayout(GameType.Pyramid, variant, cells);
        }
    }
}
