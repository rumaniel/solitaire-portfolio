using System;

namespace Model.Card
{
    /// <summary>
    /// Pure domain model representing a playing card without Unity dependencies.
    /// </summary>
    public class PlayingCard : IEquatable<PlayingCard>
    {
        public Rank Rank { get; private set; }
        public Suit Suit { get; private set; }

        public PlayingCard(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        public bool Equals(PlayingCard other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Rank == other.Rank && Suit == other.Suit;
        }

        public override bool Equals(object obj)
        {
            if (obj is PlayingCard other) return Equals(other);
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Rank, Suit);
        }
    }
}
