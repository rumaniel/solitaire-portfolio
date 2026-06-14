using System;
using System.Collections.Generic;
using NUnit.Framework;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class SolvableSeedResolverTests
    {
        private StubDealRule _rule;

        [SetUp]
        public void SetUp()
        {
            _rule = new StubDealRule();
        }

        [Test]
        public void Resolve_SameInputSeed_IsDeterministic()
        {
            var first = SolvableSeedResolver.Resolve(12345, _rule, stateBudget: 5_000, maxAttempts: 8);
            var second = SolvableSeedResolver.Resolve(12345, _rule, stateBudget: 5_000, maxAttempts: 8);

            Assert.AreEqual(first.Seed, second.Seed);
            Assert.AreEqual(first.Attempts, second.Attempts);
            Assert.AreEqual(first.Proven, second.Proven);
        }

        [Test]
        public void Resolve_ProvenResult_IsSolvableWhenReplayed()
        {
            for (int inputSeed = 0; inputSeed < 10; inputSeed++)
            {
                var resolved = SolvableSeedResolver.Resolve(inputSeed, _rule, stateBudget: 5_000, maxAttempts: 8);
                if (!resolved.Proven) continue;

                var deck = DeckFactory.CreateShuffled(resolved.Seed);
                var state = DealBuilder.Build(deck, _rule);
                var replay = KlondikeFastSolver.Solve(state, _rule, stateBudget: 5_000);

                Assert.IsTrue(replay.Solved,
                    $"Resolved seed {resolved.Seed} (input {inputSeed}) must be solvable on replay.");
            }
        }

        [Test]
        public void Resolve_ConsecutiveInputSeeds_YieldDistinctCandidates()
        {
            var seeds = new HashSet<int>();
            for (int inputSeed = 0; inputSeed < 100; inputSeed++)
            {
                // maxAttempts 1 isolates the first Mix candidate per input seed.
                var resolved = SolvableSeedResolver.Resolve(inputSeed, _rule, stateBudget: 1, maxAttempts: 1);
                seeds.Add(resolved.Seed);
            }

            Assert.AreEqual(100, seeds.Count, "First candidates of consecutive input seeds must not collide.");
        }

        [Test]
        public void Resolve_Draw3Rule_ReturnsResult()
        {
            var rule = new StubDealRule { StockDrawCount = 3 };
            var resolved = SolvableSeedResolver.Resolve(42, rule, stateBudget: 20_000, maxAttempts: 2);

            Assert.IsTrue(resolved.Attempts >= 1);
            if (resolved.Proven)
            {
                var state = DealBuilder.Build(DeckFactory.CreateShuffled(resolved.Seed), rule);
                Assert.IsTrue(KlondikeFastSolver.Solve(state, rule, stateBudget: 20_000).Solved);
            }
        }

        [Test]
        public void Resolve_NullRule_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SolvableSeedResolver.Resolve(0, null, stateBudget: 1, maxAttempts: 1));
        }

        [Test]
        public void Resolve_ZeroStateBudget_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SolvableSeedResolver.Resolve(0, _rule, stateBudget: 0, maxAttempts: 1));
        }

        [Test]
        public void Resolve_ZeroMaxAttempts_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SolvableSeedResolver.Resolve(0, _rule, stateBudget: 1, maxAttempts: 0));
        }

        [Test]
        public void Resolve_EasthavenRule_ConvenienceOverload_DoesNotThrowAndIsDeterministic()
        {
            // Easthaven: StockDealsToTableau = true → should select 50k/4 budget without throwing.
            var rule = new StubDealRule
            {
                HasWaste = false,
                CanRecycleStock = false,
                StockDealsToTableau = true,
                InitialCardCounts = new[] { 3, 3, 3, 3, 3, 3, 3 },
                InitialFaceUpPerColumn = 3,
                OnlyKingOnEmptyTableau = false,
                StockDrawCount = 0,
            };

            var first = SolvableSeedResolver.Resolve(42, rule);
            var second = SolvableSeedResolver.Resolve(42, rule);

            Assert.AreEqual(first.Seed, second.Seed, "Easthaven convenience overload must be deterministic.");
            Assert.AreEqual(first.Attempts, second.Attempts);
            Assert.GreaterOrEqual(first.Attempts, 1);
        }
    }
}
