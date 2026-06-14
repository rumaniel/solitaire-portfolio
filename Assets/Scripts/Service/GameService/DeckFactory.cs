using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Model.Card;
using Model.Game;

namespace Service.GameService
{
    /// <summary>
    /// Static factory for creating a standard 52-card deck.
    /// </summary>
    public static class DeckFactory
    {
        /// <summary>
        /// Creates a standard 52-card deck in canonical order (not shuffled).
        /// <br/>Suit: Spade / Heart / Diamond / Club (13 cards each)
        /// <br/>Rank: Ace(1) ~ King(13)
        /// </summary>
        public static List<PlayingCard> CreateOrdered()
        {
            var deck = new List<PlayingCard>(52);
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                if (suit == Suit.None) continue;
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    if (rank == Rank.None) continue;
                    deck.Add(new PlayingCard(rank, suit));
                }
            }

            return deck;
        }

        /// <summary>
        /// Creates a standard 52-card deck shuffled deterministically using the given seed.
        /// Same seed always produces the same card order (Fisher-Yates algorithm).
        /// </summary>
        public static List<PlayingCard> CreateShuffled(int seed)
        {
            var deck = CreateOrdered();
            var rng = new Random(seed);
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }

            return deck;
        }

        /// <summary>
        /// Generates a cryptographically random seed for shuffling.
        /// Uses <see cref="RandomNumberGenerator"/> for high-quality entropy.
        /// </summary>
        public static int CreateRandomSeed()
        {
            var bytes = new byte[4];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        /// <summary>
        /// Ordered deck for the rule's composition: DeckCount x 52 cards, suits remapped onto
        /// the first SuitCount suits in canonical order (ranks preserved).
        /// Spider 1-suit => 8 spade sets; 2-suit => 4 sets each of the first two suits.
        /// </summary>
        public static List<PlayingCard> CreateOrdered(IDealRule rule)
        {
            var suits = new List<Suit>(4);
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                if (suit == Suit.None) continue;
                suits.Add(suit);
            }

            // Fail fast on a misconfigured asset rather than silently dealing a wrong-sized deck:
            // DeckCount < 1 would produce an empty deck; SuitCount out of [1,4] would index past
            // the suit table — both far harder to diagnose at deal time.
            if (rule.DeckCount < 1)
                throw new ArgumentOutOfRangeException(nameof(rule.DeckCount), rule.DeckCount,
                    "DeckCount must be at least 1.");
            if (rule.SuitCount < 1 || rule.SuitCount > suits.Count)
                throw new ArgumentOutOfRangeException(nameof(rule.SuitCount), rule.SuitCount,
                    "SuitCount must be between 1 and 4.");

            var deck = new List<PlayingCard>(rule.DeckCount * 52);
            for (int d = 0; d < rule.DeckCount; d++)
            {
                for (int s = 0; s < suits.Count; s++)
                {
                    var suit = suits[s % rule.SuitCount];
                    foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                    {
                        if (rank == Rank.None) continue;
                        deck.Add(new PlayingCard(rank, suit));
                    }
                }
            }
            return deck;
        }

        /// <summary>Deterministic Fisher-Yates shuffle of the rule's deck composition.</summary>
        public static List<PlayingCard> CreateShuffled(int seed, IDealRule rule)
        {
            var deck = CreateOrdered(rule);
            var rng = new Random(seed);
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
            return deck;
        }
    }
}
