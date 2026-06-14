using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Model.Card;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class ShuffleStrategyTests
    {
        private const int TestSeed = 42;

        // --- FisherYatesShuffleStrategy ---

        [Test]
        public void FisherYates_Returns52UniqueCards()
        {
            var strategy = new FisherYatesShuffleStrategy();
            var deck = strategy.Shuffle(TestSeed);

            Assert.AreEqual(52, deck.Count);
            var distinct = new HashSet<(Rank, Suit)>(deck.Select(c => (c.Rank, c.Suit)));
            Assert.AreEqual(52, distinct.Count);
        }

        [Test]
        public void FisherYates_SameSeed_SameResult()
        {
            var strategy = new FisherYatesShuffleStrategy();
            var deck1 = strategy.Shuffle(TestSeed);
            var deck2 = strategy.Shuffle(TestSeed);

            for (int i = 0; i < deck1.Count; i++)
                Assert.AreEqual(deck1[i], deck2[i], $"Mismatch at index {i}");
        }

        [Test]
        public void FisherYates_MatchesDeckFactory()
        {
            var strategy = new FisherYatesShuffleStrategy();
            var fromStrategy = strategy.Shuffle(TestSeed);
            var fromFactory = DeckFactory.CreateShuffled(TestSeed);

            for (int i = 0; i < fromStrategy.Count; i++)
                Assert.AreEqual(fromFactory[i], fromStrategy[i], $"Mismatch at index {i}");
        }

        // --- ShuffleStrategyProvider ---

        [Test]
        public void Provider_DefaultIsFisherYates()
        {
            var provider = new ShuffleStrategyProvider();
            var deck = provider.Current.Shuffle(TestSeed);
            var expected = DeckFactory.CreateShuffled(TestSeed);

            for (int i = 0; i < deck.Count; i++)
                Assert.AreEqual(expected[i], deck[i], $"Mismatch at index {i}");
        }

        [Test]
        public void Provider_SwapStrategy_UsesNewStrategy()
        {
            var provider = new ShuffleStrategyProvider();
            provider.Current = new IdentityShuffleStrategy();

            var deck = provider.Current.Shuffle(TestSeed);
            var ordered = DeckFactory.CreateOrdered();

            for (int i = 0; i < deck.Count; i++)
                Assert.AreEqual(ordered[i], deck[i], $"Mismatch at index {i}");
        }

        // --- SolitaireGameService integration ---

        [Test]
        public void GameService_DefaultProvider_UsesFisherYates()
        {
            var provider = new ShuffleStrategyProvider();
            var service = new SolitaireGameService(provider);
            var rule = new StubDealRule();
            service.Initialize(rule, TestSeed);

            var expected = DealBuilder.Build(DeckFactory.CreateShuffled(TestSeed), rule);

            // Verify specific card positions, not just counts
            for (int col = 0; col < rule.TableauCount; col++)
            {
                var expCards = expected.Tableaus[col].Cards;
                var actCards = service.CurrentState.Tableaus[col].Cards;
                for (int i = 0; i < expCards.Count; i++)
                    Assert.AreEqual(expCards[i], actCards[i], $"Tableau[{col}][{i}] mismatch");
            }
        }

        [Test]
        public void GameService_WithProvider_UsesProviderStrategy()
        {
            var provider = new ShuffleStrategyProvider { Current = new IdentityShuffleStrategy() };
            var service = new SolitaireGameService(provider);
            var rule = new StubDealRule();
            service.Initialize(rule, TestSeed);

            var expected = DealBuilder.Build(DeckFactory.CreateOrdered(), rule);

            // Verify card positions match ordered-deck layout
            for (int col = 0; col < rule.TableauCount; col++)
            {
                var expCards = expected.Tableaus[col].Cards;
                var actCards = service.CurrentState.Tableaus[col].Cards;
                for (int i = 0; i < expCards.Count; i++)
                    Assert.AreEqual(expCards[i], actCards[i], $"Tableau[{col}][{i}] mismatch");
            }
        }

        [Test]
        public void GameService_WithSolvedProvider_UsesSolvedStrategy()
        {
            var provider = new ShuffleStrategyProvider
            {
                Current = new SolvedShuffleStrategy(extraShuffles: 0)
            };
            var service = new SolitaireGameService(provider);
            var rule = new StubDealRule();
            service.Initialize(rule, TestSeed);

            var expected = DealBuilder.Build(
                new SolvedShuffleStrategy(extraShuffles: 0).Shuffle(TestSeed),
                rule);

            for (int col = 0; col < rule.TableauCount; col++)
            {
                var expCards = expected.Tableaus[col].Cards;
                var actCards = service.CurrentState.Tableaus[col].Cards;
                for (int i = 0; i < expCards.Count; i++)
                    Assert.AreEqual(expCards[i], actCards[i], $"Tableau[{col}][{i}] mismatch");
            }
        }

        // --- SolvedShuffleStrategy ---

        [Test]
        public void Solved_Returns52UniqueCards()
        {
            var strategy = new SolvedShuffleStrategy();
            var deck = strategy.Shuffle(TestSeed);

            Assert.AreEqual(52, deck.Count);
            var distinct = new HashSet<(Rank, Suit)>(deck.Select(c => (c.Rank, c.Suit)));
            Assert.AreEqual(52, distinct.Count);
        }

        [Test]
        public void Solved_SameSeed_SameResult()
        {
            var strategy = new SolvedShuffleStrategy(extraShuffles: 10);
            var deck1 = strategy.Shuffle(TestSeed);
            var deck2 = strategy.Shuffle(TestSeed);

            for (int i = 0; i < deck1.Count; i++)
                Assert.AreEqual(deck1[i], deck2[i], $"Mismatch at index {i}");
        }

        [Test]
        public void Solved_ZeroExtraShuffles_HighRanksFirst()
        {
            var strategy = new SolvedShuffleStrategy(extraShuffles: 0);
            var deck = strategy.Shuffle(TestSeed);

            // First 4 cards should all be Kings (rank-descending, 4 suits per rank)
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(Rank.King, deck[i].Rank, $"deck[{i}] should be King");

            // Last 4 cards should all be Aces
            for (int i = 48; i < 52; i++)
                Assert.AreEqual(Rank.Ace, deck[i].Rank, $"deck[{i}] should be Ace");
        }

        [Test]
        public void Solved_ZeroExtraShuffles_AlternatingColors()
        {
            var strategy = new SolvedShuffleStrategy(extraShuffles: 0);
            var deck = strategy.Shuffle(TestSeed);

            // Within each rank group of 4, colors should alternate
            for (int rank = 0; rank < 13; rank++)
            {
                int baseIdx = rank * 4;
                for (int i = 0; i < 3; i++)
                {
                    bool currentIsRed = IsRed(deck[baseIdx + i].Suit);
                    bool nextIsRed = IsRed(deck[baseIdx + i + 1].Suit);
                    Assert.AreNotEqual(currentIsRed, nextIsRed,
                        $"deck[{baseIdx + i}] and deck[{baseIdx + i + 1}] should alternate colors");
                }
            }
        }

        [Test]
        public void Solved_StockHasAcesOnTop()
        {
            // Klondike: 28 cards to tableaus, remaining 24 to stock.
            // Stock draws from end → last cards in deck are drawn first.
            // With 0 extra shuffles, last 4 cards are Aces.
            var strategy = new SolvedShuffleStrategy(extraShuffles: 0);
            var deck = strategy.Shuffle(TestSeed);
            var rule = new StubDealRule();

            var state = DealBuilder.Build(deck, rule);
            var stock = state.Stock.Cards;

            // Top of stock (last element) should be an Ace
            Assert.AreEqual(Rank.Ace, stock[stock.Count - 1].Rank);
        }

        [Test]
        public void Solved_ExtraShuffles_ChangesLayout()
        {
            var clean = new SolvedShuffleStrategy(extraShuffles: 0).Shuffle(TestSeed);
            var shuffled = new SolvedShuffleStrategy(extraShuffles: 50).Shuffle(TestSeed);

            bool anyDifferent = clean.Where((card, i) => !card.Equals(shuffled[i])).Any();
            Assert.IsTrue(anyDifferent, "Extra shuffles should change the deck order");
        }

        [Test]
        public void Solved_GameService_InitializesSuccessfully()
        {
            var provider = new ShuffleStrategyProvider { Current = new SolvedShuffleStrategy() };
            var service = new SolitaireGameService(provider);
            var rule = new StubDealRule();
            service.Initialize(rule, TestSeed);

            Assert.IsNotNull(service.CurrentState);
            Assert.AreEqual(7, service.CurrentState.Tableaus.Count);
            Assert.AreEqual(4, service.CurrentState.Foundations.Count);
        }

        // --- Test utility ---

        private static bool IsRed(Suit suit) => suit == Suit.Heart || suit == Suit.Diamond;

        private class IdentityShuffleStrategy : IShuffleStrategy
        {
            public List<PlayingCard> Shuffle(int seed) => DeckFactory.CreateOrdered();
        }
    }
}
