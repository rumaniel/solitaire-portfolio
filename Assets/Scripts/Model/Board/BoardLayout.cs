using System;
using System.Collections.Generic;
using Model.Game;

namespace Model.Board
{
    /// <summary>
    /// Immutable board topology for a (GameType, Variant). Built by code (Pyramid) or loaded from an
    /// asset (Mahjong, later). Configuration object — equality is not required (analogous to IDealRule).
    /// </summary>
    public sealed class BoardLayout
    {
        public GameType GameType { get; }
        public int Variant { get; }
        public IReadOnlyList<BoardCell> Cells { get; }

        private readonly Dictionary<CellId, BoardCell> byId;

        public BoardLayout(GameType gameType, int variant, IReadOnlyList<BoardCell> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            GameType = gameType;
            Variant = variant;

            // Defensive copy + duplicate-id detection: an immutable topology must not silently shadow cells.
            var copy = new List<BoardCell>(cells.Count);
            byId = new Dictionary<CellId, BoardCell>(cells.Count);
            foreach (var cell in cells)
            {
                if (cell == null)
                    throw new ArgumentException("Layout contains a null cell.", nameof(cells));
                if (byId.ContainsKey(cell.Id))
                    throw new ArgumentException($"Duplicate CellId {cell.Id} in layout.", nameof(cells));
                byId[cell.Id] = cell;
                copy.Add(cell);
            }

            // BoardState indexes its card array directly by CellId.Value, so ids MUST be the dense set
            // 0..Count-1 and every CoverBlocker must reference a real cell. Enforce it here so a malformed
            // layout (hand-authored, data-loaded, or migrated) fails at construction, not mid-game.
            int count = copy.Count;
            foreach (var cell in copy)
            {
                if (cell.Id.Value < 0 || cell.Id.Value >= count)
                    throw new ArgumentException(
                        $"CellId {cell.Id} is outside the dense range [0, {count}). Cell ids must be 0..Count-1.",
                        nameof(cells));
                foreach (var blocker in cell.CoverBlockers)
                {
                    if (!byId.ContainsKey(blocker))
                        throw new ArgumentException(
                            $"Cell {cell.Id} has a cover-blocker {blocker} that is not a cell in this layout.",
                            nameof(cells));
                }
            }

            Cells = copy.AsReadOnly();
        }

        public int Count => Cells.Count;
        public BoardCell Cell(CellId id) => byId[id];
        public bool TryGetCell(CellId id, out BoardCell cell) => byId.TryGetValue(id, out cell);
    }
}
