using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Model.Card;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class DeckFactoryTests
    {
        // --- CreateOrdered ---

        [Test]
        public void CreateOrdered_Returns52Cards()
        {
            var deck = DeckFactory.CreateOrdered();
            Assert.AreEqual(52, deck.Count);
        }

        [Test]
        public void CreateOrdered_NoDuplicates()
        {
            var deck = DeckFactory.CreateOrdered();
            var distinct = new HashSet<(Rank, Suit)>(deck.Select(c => (c.Rank, c.Suit)));
            Assert.AreEqual(52, distinct.Count);
        }

        // --- CreateShuffled ---

        [Test]
        public void CreateShuffled_Returns52Cards()
        {
            var deck = DeckFactory.CreateShuffled(12345);
            Assert.AreEqual(52, deck.Count);
        }

        [Test]
        public void CreateShuffled_NoDuplicates()
        {
            var deck = DeckFactory.CreateShuffled(12345);
            var distinct = new HashSet<(Rank, Suit)>(deck.Select(c => (c.Rank, c.Suit)));
            Assert.AreEqual(52, distinct.Count);
        }

        [Test]
        public void CreateShuffled_SameSeed_SameResult()
        {
            var deck1 = DeckFactory.CreateShuffled(42);
            var deck2 = DeckFactory.CreateShuffled(42);

            Assert.AreEqual(deck1.Count, deck2.Count);
            for (int i = 0; i < deck1.Count; i++)
            {
                Assert.AreEqual(deck1[i].Rank, deck2[i].Rank, $"Mismatch at index {i}");
                Assert.AreEqual(deck1[i].Suit, deck2[i].Suit, $"Mismatch at index {i}");
            }
        }

        [Test]
        public void CreateShuffled_DifferentSeed_DifferentResult()
        {
            var deck1 = DeckFactory.CreateShuffled(1);
            var deck2 = DeckFactory.CreateShuffled(2);

            // At least one card should differ (extremely high probability)
            bool anyDifferent = deck1.Where((card, i) => !card.Equals(deck2[i])).Any();
            Assert.IsTrue(anyDifferent, "Two different seeds produced identical decks");
        }

        [Test]
        public void CreateShuffled_NegativeSeed_Works()
        {
            var deck = DeckFactory.CreateShuffled(-999);
            Assert.AreEqual(52, deck.Count);
        }

        // --- CreateRandomSeed ---

        [Test]
        public void CreateRandomSeed_ProducesDeterministicShuffleWhenUsedAsSeed()
        {
            // Verify that a crypto-generated seed can be fed back into CreateShuffled
            // and produces the same deck both times (proves the seed is a valid int).
            int seed = DeckFactory.CreateRandomSeed();
            var deck1 = DeckFactory.CreateShuffled(seed);
            var deck2 = DeckFactory.CreateShuffled(seed);

            for (int i = 0; i < deck1.Count; i++)
                Assert.AreEqual(deck1[i], deck2[i], $"Mismatch at index {i} for seed {seed}");
        }

        [Test]
        public void CreateShuffled_SpiderOneSuit_Produces104Spades_EightPerRank()
        {
            var rule = new StubDealRule { DeckCountOverride = 2, SuitCountOverride = 1 };
            var deck = DeckFactory.CreateShuffled(7, rule);

            Assert.AreEqual(104, deck.Count);
            Assert.IsTrue(deck.All(c => c.Suit == Suit.Spade));
            foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
            {
                if (rank == Rank.None) continue;
                Assert.AreEqual(8, deck.Count(c => c.Rank == rank), $"rank {rank}");
            }
        }

        [Test]
        public void CreateShuffled_TwoDecksFourSuits_Produces104_TwoPerCard()
        {
            var rule = new StubDealRule { DeckCountOverride = 2, SuitCountOverride = 4 };
            var deck = DeckFactory.CreateShuffled(7, rule);

            Assert.AreEqual(104, deck.Count);
            Assert.AreEqual(2, deck.Count(c => c.Suit == Suit.Spade && c.Rank == Rank.Ace));
        }

        [Test]
        public void CreateShuffled_RuleOverload_IsDeterministicPerSeed()
        {
            var rule = new StubDealRule { DeckCountOverride = 2, SuitCountOverride = 1 };
            CollectionAssert.AreEqual(
                DeckFactory.CreateShuffled(42, rule),
                DeckFactory.CreateShuffled(42, rule));
        }

        [Test]
        public void CreateShuffled_DefaultRule_MatchesLegacyOverload()
        {
            // The single-deck path must stay byte-identical: GameCode replays depend on it.
            var rule = new StubDealRule();
            CollectionAssert.AreEqual(
                DeckFactory.CreateShuffled(123),
                DeckFactory.CreateShuffled(123, rule));
        }
    }
}
