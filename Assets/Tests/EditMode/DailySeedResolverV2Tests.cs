using System;
using NUnit.Framework;
using Model.Game;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class DailySeedResolverV2Tests
    {
        // Five consecutive dates used for determinism and golden-pin assertions.
        private static readonly DateTime[] Dates =
        {
            new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc),
        };

        private StubDealRule _rule;

        [SetUp]
        public void SetUp()
        {
            // Draw-1 Klondike rule — mirrors the pinned daily contract.
            _rule = new StubDealRule { StockDrawCount = 1 };
        }

        [Test]
        public void ResolveForKlondikeDrawOne_SameDate_IsDeterministic()
        {
            foreach (var date in Dates)
            {
                int first  = DailySeedResolverV2.ResolveForKlondikeDrawOne(date);
                int second = DailySeedResolverV2.ResolveForKlondikeDrawOne(date);
                Assert.AreEqual(first, second,
                    $"Resolved seed must be identical on repeated calls for {date:yyyy-MM-dd}.");
            }
        }

        [Test]
        public void ResolveFor_WithExplicitRule_IsDeterministic()
        {
            foreach (var date in Dates)
            {
                int first  = DailySeedResolverV2.ResolveFor(date, Model.Game.GameType.Klondike, _rule);
                int second = DailySeedResolverV2.ResolveFor(date, Model.Game.GameType.Klondike, _rule);
                Assert.AreEqual(first, second,
                    $"Resolved seed must be identical on repeated calls for {date:yyyy-MM-dd}.");
            }
        }

        [Test]
        public void ResolveForKlondikeDrawOne_AllDates_ProduceSolverVerifiedDeals()
        {
            foreach (var date in Dates)
            {
                int resolved = DailySeedResolverV2.ResolveForKlondikeDrawOne(date);
                var deck     = DeckFactory.CreateShuffled(resolved);
                var state    = DealBuilder.Build(deck, _rule);
                var result   = KlondikeFastSolver.Solve(state, _rule, stateBudget: 5_000);

                Assert.IsTrue(result.Solved,
                    $"Resolved seed {resolved} for {date:yyyy-MM-dd} must be solver-proven solvable.");
            }
        }

        /// <summary>
        /// Contract-link test: the frozen Klondike draw-1 rule must have identical values to
        /// the live DealRuleAsset. If they diverge, the daily path and the normal Klondike path
        /// would resolve deals under different rules. Load the real asset via AssetDatabase so
        /// any future change to the ScriptableObject data is caught immediately.
        /// </summary>
        [Test]
        public void FrozenKlondikeDrawOneRule_MatchesLiveAsset()
        {
            const string assetPath = "Assets/ScriptableObjects/DealRule/Klondike Draw1.asset";
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Data.Game.DealRuleAsset>(assetPath);
            Assert.IsNotNull(asset,
                $"Could not load DealRuleAsset at '{assetPath}'. " +
                "If the asset was moved, update the path in this test.");

            IDealRule frozen = DailySeedResolverV2.FrozenKlondikeDrawOneRuleInstance;

            Assert.AreEqual(asset.TableauCount,           frozen.TableauCount,           nameof(IDealRule.TableauCount));
            Assert.AreEqual(asset.FoundationCount,        frozen.FoundationCount,        nameof(IDealRule.FoundationCount));
            Assert.AreEqual(asset.PerSuitCardCount,       frozen.PerSuitCardCount,       nameof(IDealRule.PerSuitCardCount));
            Assert.AreEqual(asset.HasWaste,               frozen.HasWaste,               nameof(IDealRule.HasWaste));
            Assert.AreEqual(asset.CanRecycleStock,        frozen.CanRecycleStock,        nameof(IDealRule.CanRecycleStock));
            Assert.AreEqual(asset.StockDrawCount,         frozen.StockDrawCount,         nameof(IDealRule.StockDrawCount));
            Assert.AreEqual(asset.StockDealsToTableau,    frozen.StockDealsToTableau,    nameof(IDealRule.StockDealsToTableau));
            Assert.AreEqual(asset.InitialFaceUpPerColumn, frozen.InitialFaceUpPerColumn, nameof(IDealRule.InitialFaceUpPerColumn));
            Assert.AreEqual(asset.OnlyKingOnEmptyTableau, frozen.OnlyKingOnEmptyTableau, nameof(IDealRule.OnlyKingOnEmptyTableau));

            // InitialCardCounts is an array — compare element-wise.
            int[] assetCounts  = asset.InitialCardCounts;
            int[] frozenCounts = frozen.InitialCardCounts;
            Assert.AreEqual(assetCounts.Length, frozenCounts.Length, "InitialCardCounts length mismatch.");
            for (int i = 0; i < assetCounts.Length; i++)
                Assert.AreEqual(assetCounts[i], frozenCounts[i], $"InitialCardCounts[{i}] mismatch.");
        }

        /// <summary>
        /// Proves that sliced execution (stepping the solver 1024 states at a time without
        /// yielding) produces the same resolved seed as the one-shot sync path for each of the
        /// five pinned dates. This is the synchronous bridge between the async PlayerLoop path
        /// and the golden-pinned sync path: if this passes, introducing a yield point between
        /// slices cannot change daily determinism (it only changes scheduling, not state).
        /// No UniTask is awaited — the stepping loop runs synchronously.
        /// </summary>
        [Test]
        public void ResolveForKlondikeDrawOne_SteppedPath_MatchesSyncPath_ForFiveDates()
        {
            const int stateBudget = 5_000;   // same pinned constant as DailySeedResolverV2
            const int maxAttempts = 8;        // same pinned constant as DailySeedResolverV2
            const int sliceSize = 1_024;

            var frozenRule = DailySeedResolverV2.FrozenKlondikeDrawOneRuleInstance;

            foreach (var date in Dates)
            {
                int inputSeed = DailySeed.For(date, Model.Game.GameType.Klondike);

                // Sync one-shot path (the golden contract).
                int syncSeed = SolvableSeedResolver.Resolve(inputSeed, frozenRule, stateBudget, maxAttempts).Seed;

                // Stepped path: same loop as ResolveAsync but without yields — proves that
                // the slice boundary does not alter exploration order or budget semantics.
                int candidate = 0;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    candidate = SeedMix.Mix(inputSeed, attempt);
                    var deck = DeckFactory.CreateShuffled(candidate);
                    var state = DealBuilder.Build(deck, frozenRule);
                    var r = KlondikeFastSolver.SolveStepped(state, frozenRule, stateBudget, sliceSize);
                    if (r.Solved) break;
                }
                int steppedSeed = candidate;

                Assert.AreEqual(syncSeed, steppedSeed,
                    $"Stepped path diverged from sync path for {date:yyyy-MM-dd}: " +
                    $"sync={syncSeed}, stepped={steppedSeed}. " +
                    "A slice-boundary bug would change which attempt is first proven solvable.");
            }
        }

        /// <summary>
        /// GOLDEN PIN — these literals freeze the daily v2 resolve chain. Every client
        /// must derive the identical deal for a given date, so if a solver or resolver
        /// change alters any of these values, do NOT update the literals: introduce a
        /// new versioned resolver entry point instead.
        /// </summary>
        [Test]
        public void ResolveForKlondikeDrawOne_GoldenSeeds_MatchPinnedConstants()
        {
            // Captured 2026-06-11 from the first pinned Unity run.
            int[] goldenSeeds = { -849988480, 2145067663, 1135542124, 335142626, 2008897580 };

            var actual = new int[Dates.Length];
            for (int i = 0; i < Dates.Length; i++)
                actual[i] = DailySeedResolverV2.ResolveForKlondikeDrawOne(Dates[i]);

            Assert.AreEqual(goldenSeeds.Length, actual.Length);
            for (int i = 0; i < goldenSeeds.Length; i++)
            {
                Assert.AreEqual(goldenSeeds[i], actual[i],
                    $"Golden seed mismatch for {Dates[i]:yyyy-MM-dd}: " +
                    $"expected {goldenSeeds[i]}, got {actual[i]}. " +
                    "A solver change altered verdicts — bump to a new versioned resolver entry point.");
            }
        }
    }
}
