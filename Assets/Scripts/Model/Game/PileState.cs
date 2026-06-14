using System;
using System.Collections.Generic;
using Model.Card;

namespace Model.Game
{
    /// <summary>
    /// Value object representing the state of a single pile.
    /// Holds the card list together with the face-up/face-down boundary index.
    /// </summary>
    public class PileState : IEquatable<PileState>
    {
        /// <summary>Identifier of this pile (PileType + Index).</summary>
        public PileId Id { get; }

        /// <summary>
        /// Card list (index 0 = bottom). Read-only.
        /// </summary>
        public IReadOnlyList<PlayingCard> Cards { get; }

        /// <summary>
        /// Cards at or above this index are face-up; cards below are face-down.
        /// <br/>Equals Cards.Count when the pile is empty or all cards are face-down.
        /// </summary>
        public int FaceUpFromIndex { get; }

        public PileState(PileId id, List<PlayingCard> cards, int faceUpFromIndex)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));

            Id = id;
            Cards = cards.AsReadOnly();
            FaceUpFromIndex = faceUpFromIndex;
        }

        /// <summary>Returns whether the card at the given index is face-up.</summary>
        public bool IsFaceUp(int index) => index >= FaceUpFromIndex;

        public bool Equals(PileState other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (!Id.Equals(other.Id)) return false;
            if (FaceUpFromIndex != other.FaceUpFromIndex) return false;
            if (Cards.Count != other.Cards.Count) return false;

            for (int i = 0; i < Cards.Count; i++)
            {
                if (!Cards[i].Equals(other.Cards[i])) return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is PileState other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(Id, FaceUpFromIndex, Cards.Count);
            for (int i = 0; i < Cards.Count; i++)
            {
                hash = HashCode.Combine(hash, Cards[i]);
            }

            return hash;
        }
    }
}
