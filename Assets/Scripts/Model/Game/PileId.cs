using System;
using Model.Card;

namespace Model.Game
{
    /// <summary>
    /// Represents a unique identifier for a pile in the game, combining pile type, index,
    /// and optionally the top card for more specific identification.
    /// This allows for precise tracking of piles, especially when multiple piles of the same type exist.
    /// The Card property can be used to identify the specific pile when multiple piles of the same type are present,
    /// or to track the top card of a pile for game logic purposes.
    /// </summary>
    public readonly struct PileId : IEquatable<PileId>
    {
        public PileType Type { get; }
        public int Index { get; }
        public PlayingCard Card { get; }

        public PileId(PileType type, int index)
        {
            Type = type;
            Index = index;
            Card = new PlayingCard(Rank.None, Suit.None); // Placeholder card for empty piles
        }

        public PileId(PileType type, int index, PlayingCard card)
        {
            Type = type;
            Index = index;
            Card = card;
        }

        public bool Equals(PileId other)
        {
            return Type == other.Type && Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return obj is PileId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Index);
        }

        public override string ToString()
        {
            return $"PileId(Type: {Type}, Index: {Index}, Card: {Card})";
        }
    }
}
