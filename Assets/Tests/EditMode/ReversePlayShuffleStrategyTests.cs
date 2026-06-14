using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Model.Card;
using Model.Game;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class ReversePlayShuffleStrategyTests
    {
        private const int TestSeed = 12345;

        private StubDealRule _rule;
        private ReversePlayShuffleStrategy _strategy;

        [SetUp]
        public void SetUp()
        {
            _rule = new StubDealRule();
            _strategy = new ReversePlayShuffleStrategy();
        }

        [Test]
        public void BuildInitialState_ReturnsNonNull()
        {
            var state = _strategy.BuildInitialState(TestSeed, _rule);
            Assert.IsNotNull(state);
        }

        [Test]
        public void BuildInitialState_SameSeed_IdenticalState()
        {
            var a = _strategy.BuildInitialState(TestSeed, _rule);
            var b = _strategy.BuildInitialState(TestSeed, _rule);

            Assert.AreEqual(a.Stock, b.Stock);
            Assert.AreEqual(a.Waste, b.Waste);
            Assert.AreEqual(a.Foundations.Count, b.Foundations.Count);
            for (int i = 0; i < a.Foundations.Count; i++)
                Assert.AreEqual(a.Foundations[i], b.Foundations[i]);
            Assert.AreEqual(a.Tableaus.Count, b.Tableaus.Count);
            for (int i = 0; i < a.Tableaus.Count; i++)
                Assert.AreEqual(a.Tableaus[i], b.Tableaus[i]);
        }

        [Test]
        public void Phase2_TableauMatchesTriangleShape()
        {
            var state = _strategy.BuildInitialState(TestSeed, _rule);

            for (int c = 0; c < _rule.TableauCount; c++)
            {
                Assert.AreEqual(_rule.InitialCardCounts[c], state.Tableaus[c].Cards.Count,
                    $"Tableau column {c} should have {_rule.InitialCardCounts[c]} cards.");
            }
        }

        [Test]
        public void Phase2_FoundationsAndWasteAreEmpty()
        {
            var state = _strategy.BuildInitialState(TestSeed, _rule);

            foreach (var f in state.Foundations)
                Assert.AreEqual(0, f.Cards.Count, "Foundation should be empty after Phase 2.");
            Assert.AreEqual(0, state.Waste.Cards.Count, "Waste should be empty after Phase 2.");
        }

        [Test]
        public void Phase2_StockPlusTableauEquals52()
        {
            var state = _strategy.BuildInitialState(TestSeed, _rule);
            int totalTableau = state.Tableaus.Sum(t => t.Cards.Count);
            Assert.AreEqual(52, totalTableau + state.Stock.Cards.Count);
        }

        [Test]
        public void Phase3_StockHoldsExactly24Cards()
        {
            var state = _strategy.BuildInitialState(TestSeed, _rule);
            Assert.AreEqual(24, state.Stock.Cards.Count);
        }

        [Test]
        public void Phase4_EachColumnHasOnlyLastCardFaceUp()
        {
            var state = _strategy.BuildInitialState(TestSeed, _rule);
            for (int c = 0; c < state.Tableaus.Count; c++)
            {
                var pile = state.Tableaus[c];
                Assert.AreEqual(pile.Cards.Count - 1, pile.FaceUpFromIndex,
                    $"Column {c} should have only its last card face-up.");
            }
        }

        [Test]
        public void Phase4_StockIsAllFaceDown()
        {
            var state = _strategy.BuildInitialState(TestSeed, _rule);
            Assert.AreEqual(state.Stock.Cards.Count, state.Stock.FaceUpFromIndex);
        }

        [Test]
        public void Integration_MimicsDealBuilderShape()
        {
            // Compare against DealBuilder.Build output shape — same invariants must hold.
            var state = _strategy.BuildInitialState(TestSeed, _rule);
            var baseline = DealBuilder.Build(DeckFactory.CreateShuffled(42), _rule);

            Assert.AreEqual(baseline.Tableaus.Count, state.Tableaus.Count);
            Assert.AreEqual(baseline.Foundations.Count, state.Foundations.Count);
            for (int c = 0; c < baseline.Tableaus.Count; c++)
                Assert.AreEqual(baseline.Tableaus[c].Cards.Count, state.Tableaus[c].Cards.Count);
            Assert.AreEqual(baseline.Stock.Cards.Count, state.Stock.Cards.Count);
            Assert.AreEqual(baseline.Waste.Cards.Count, state.Waste.Cards.Count);
        }

        [Test]
        public void FaceUpRankDistribution_IsNotFixed()
        {
            // Regression guard against the previous strategy which locked the
            // visible tops to K/K/Q/J/10/8/7 regardless of seed.
            var rankSets = new HashSet<string>();
            for (int seed = 0; seed < 30; seed++)
            {
                var state = _strategy.BuildInitialState(seed, _rule);
                var pattern = string.Join(",", state.Tableaus.Select(t =>
                    t.Cards.Count > 0 ? ((int)t.Cards[t.Cards.Count - 1].Rank).ToString() : "0"));
                rankSets.Add(pattern);
            }
            Assert.Greater(rankSets.Count, 10,
                "30 seeds should produce more than 10 distinct face-up rank patterns.");
        }

        [Test]
        public void BuildInitialState_ContainsAll52UniqueCards()
        {
            var state = _strategy.BuildInitialState(TestSeed, _rule);
            var all = new List<PlayingCard>();
            foreach (var pile in state.Tableaus) all.AddRange(pile.Cards);
            all.AddRange(state.Stock.Cards);
            all.AddRange(state.Waste.Cards);
            foreach (var pile in state.Foundations) all.AddRange(pile.Cards);

            Assert.AreEqual(52, all.Count);
            Assert.AreEqual(52, all.Distinct().Count());
        }

        [Test]
        public void Shuffle_IShuffleStrategyPath_ReturnsSame52Cards()
        {
            // Compatibility path: the class also implements IShuffleStrategy.
            IShuffleStrategy shuffle = _strategy;
            var deck = shuffle.Shuffle(TestSeed);

            Assert.AreEqual(52, deck.Count);
            Assert.AreEqual(52, deck.Distinct().Count());
        }
    }
}
