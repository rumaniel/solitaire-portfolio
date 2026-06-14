using System;
using System.Collections.Generic;
using Model.Card;

namespace Model.Board
{
    /// <summary>
    /// Immutable runtime board state. Each cell holds a card or is empty (removed). Optional stock/waste
    /// for games that use them (Pyramid); Mahjong leaves them empty. Mutations return new instances.
    /// </summary>
    public sealed class BoardState : IEquatable<BoardState>
    {
        private readonly PlayingCard[] cells; // index = CellId.Value; null = removed

        public IReadOnlyList<PlayingCard> Stock { get; }
        public IReadOnlyList<PlayingCard> Waste { get; }

        /// <summary>How many times the waste has been recycled back into the stock (Pyramid pass count).</summary>
        public int RecycleCount { get; }

        public BoardState(IReadOnlyList<PlayingCard> cellCards,
            IReadOnlyList<PlayingCard> stock = null, IReadOnlyList<PlayingCard> waste = null, int recycleCount = 0)
        {
            if (cellCards == null) throw new ArgumentNullException(nameof(cellCards));
            cells = new PlayingCard[cellCards.Count];
            for (int i = 0; i < cellCards.Count; i++) cells[i] = cellCards[i];
            Stock = AsImmutable(stock);
            Waste = AsImmutable(waste);
            RecycleCount = recycleCount;
        }

        // Cells is never mutated in place (WithCellsRemoved clones before editing), so derived states may
        // share the array. stock/waste are always already-immutable wrappers when this is called.
        private BoardState(PlayingCard[] sharedCells, IReadOnlyList<PlayingCard> stock, IReadOnlyList<PlayingCard> waste, int recycleCount)
        {
            cells = sharedCells;
            Stock = stock;
            Waste = waste;
            RecycleCount = recycleCount;
        }

        private static readonly IReadOnlyList<PlayingCard> EmptyCards = new List<PlayingCard>(0).AsReadOnly();

        /// <summary>Defensive read-only copy so an external caller cannot mutate the stock/waste of a published state.</summary>
        private static IReadOnlyList<PlayingCard> AsImmutable(IReadOnlyList<PlayingCard> cards)
            => cards == null || cards.Count == 0 ? EmptyCards : new List<PlayingCard>(cards).AsReadOnly();

        public int CellCount => cells.Length;
        public bool HasCard(CellId id) => cells[id.Value] != null;
        public PlayingCard CardAt(CellId id) => cells[id.Value];
        public PlayingCard WasteTop => Waste.Count > 0 ? Waste[Waste.Count - 1] : null;

        public IEnumerable<CellId> OccupiedCells()
        {
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] != null) yield return new CellId(i);
        }

        public bool AnyOccupied()
        {
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] != null) return true;
            return false;
        }

        public BoardState WithCellsRemoved(IEnumerable<CellId> ids)
        {
            var copy = (PlayingCard[])cells.Clone();
            foreach (var id in ids) copy[id.Value] = null;
            return new BoardState(copy, Stock, Waste, RecycleCount);
        }

        public BoardState WithWasteTopRemoved()
        {
            if (Waste.Count == 0) return this;
            var newWaste = new List<PlayingCard>(Waste);
            newWaste.RemoveAt(newWaste.Count - 1);
            // Cells unchanged → share the array (never mutated in place); wrap waste read-only.
            return new BoardState(cells, Stock, newWaste.AsReadOnly(), RecycleCount);
        }

        /// <summary>Plays a free cell's card onto the waste (TriPeaks): the cell clears and that card
        /// becomes the new waste-top. No-op if the cell is already empty.</summary>
        public BoardState WithCardPlayedToWaste(CellId id)
        {
            var card = cells[id.Value];
            if (card == null) return this;
            var copy = (PlayingCard[])cells.Clone();
            copy[id.Value] = null;
            var newWaste = new List<PlayingCard>(Waste) { card };
            return new BoardState(copy, Stock, newWaste.AsReadOnly(), RecycleCount);
        }

        public BoardState WithStockDrawn()
        {
            if (Stock.Count == 0) return this;
            var newStock = new List<PlayingCard>(Stock);
            var top = newStock[newStock.Count - 1];
            newStock.RemoveAt(newStock.Count - 1);
            var newWaste = new List<PlayingCard>(Waste) { top };
            return new BoardState(cells, newStock.AsReadOnly(), newWaste.AsReadOnly(), RecycleCount);
        }

        /// <summary>Moves the whole waste back into the stock (reversed so it replays in draw order) and
        /// increments the recycle count. Used when the stock empties and recycling is still allowed.</summary>
        public BoardState WithStockRecycled()
        {
            if (Waste.Count == 0) return this;
            var newStock = new List<PlayingCard>(Waste);
            newStock.Reverse();
            return new BoardState(cells, newStock.AsReadOnly(), EmptyCards, RecycleCount + 1);
        }

        public bool Equals(BoardState other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (cells.Length != other.cells.Length) return false;
            for (int i = 0; i < cells.Length; i++)
            {
                var a = cells[i];
                var b = other.cells[i];
                if (a is null != (b is null)) return false;
                if (a != null && !a.Equals(b)) return false;
            }
            return RecycleCount == other.RecycleCount && ListEquals(Stock, other.Stock) && ListEquals(Waste, other.Waste);
        }

        private static bool ListEquals(IReadOnlyList<PlayingCard> a, IReadOnlyList<PlayingCard> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!a[i].Equals(b[i])) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is BoardState other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var c in cells) hash.Add(c);
            foreach (var c in Stock) hash.Add(c);
            foreach (var c in Waste) hash.Add(c);
            hash.Add(RecycleCount);
            return hash.ToHashCode();
        }
    }
}
