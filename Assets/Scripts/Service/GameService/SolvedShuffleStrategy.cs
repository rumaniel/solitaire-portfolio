using System;
using System.Collections.Generic;
using Model.Card;

namespace Service.GameService
{
    /// <summary>
    /// Builds a deck from "solved state" order — rank-descending, suits interleaved
    /// by alternating colors. When dealt by <see cref="DealBuilder"/>:
    /// <list type="bullet">
    ///   <item>Tableaus receive high-rank cards (Kings, Queens, …)</item>
    ///   <item>Stock receives low-rank cards with Aces on top (drawn first)</item>
    /// </list>
    /// <paramref name="extraShuffles"/> random pairwise swaps are applied afterward
    /// to control difficulty (0 = trivially solvable, higher = harder).
    /// </summary>
    public class SolvedShuffleStrategy : IShuffleStrategy
    {
        private readonly int extraShuffles;

        public SolvedShuffleStrategy(int extraShuffles = 0)
        {
            this.extraShuffles = Math.Max(0, extraShuffles);
        }

        public List<PlayingCard> Shuffle(int seed)
        {
            var rng = new Random(seed);

            var suits = BuildSuitOrder(rng);

            // King → Ace, cycling through the seed-determined suit order.
            // e.g. [K♠,K♥,K♣,K♦, Q♠,Q♥,Q♣,Q♦, …] (actual suits vary by seed).
            // DealBuilder fills tableaus first (28 cards), stock gets the rest (24).
            // Stock draws from the end → Aces are drawn first → easy foundation start.
            var deck = new List<PlayingCard>(52);
            for (int rank = (int)Rank.King; rank >= (int)Rank.Ace; rank--)
            {
                foreach (var suit in suits)
                    deck.Add(new PlayingCard((Rank)rank, suit));
            }

            // Extra swaps for difficulty: 0 = trivial, more = harder.
            for (int i = 0; i < extraShuffles; i++)
            {
                int a = rng.Next(deck.Count);
                int b = rng.Next(deck.Count - 1);
                if (b >= a) b++;
                (deck[a], deck[b]) = (deck[b], deck[a]);
            }

            return deck;
        }

        /// <summary>
        /// Returns 4 suits in alternating-color order, seed-determined.
        /// e.g. [♠,♥,♣,♦] or [♥,♣,♦,♠] etc.
        /// </summary>
        private static Suit[] BuildSuitOrder(Random rng)
        {
            var blacks = new[] { Suit.Spade, Suit.Club };
            var reds = new[] { Suit.Heart, Suit.Diamond };

            if (rng.Next(2) == 1) (blacks[0], blacks[1]) = (blacks[1], blacks[0]);
            if (rng.Next(2) == 1) (reds[0], reds[1]) = (reds[1], reds[0]);

            return rng.Next(2) == 0
                ? new[] { blacks[0], reds[0], blacks[1], reds[1] }
                : new[] { reds[0], blacks[0], reds[1], blacks[1] };
        }
    }
}
