using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Model.Game;

namespace Service.GameService
{
    /// <summary>
    /// Frozen daily resolve chain (v2): DailySeed -> SolvableSeedResolver with pinned
    /// budget/attempts. Every client must derive the identical deal for a given date,
    /// so these parameters and the underlying solver semantics are part of a frozen
    /// contract — guarded by golden tests; changing observed outputs requires a new
    /// versioned entry point, never an edit to this one.
    /// </summary>
    public static class DailySeedResolverV2
    {
        private const int StateBudget = 5_000;   // pinned — daily is draw-1 only
        private const int MaxAttempts = 8;        // pinned

        /// <summary>
        /// Resolves a daily seed using the provided deal rule. Prefer the no-rule overload
        /// for Klondike draw-1 (the standard daily variant).
        /// </summary>
        public static int ResolveFor(DateTime utcDate, GameType gameType, IDealRule rule)
            => SolvableSeedResolver.Resolve(
                DailySeed.For(utcDate, gameType), rule, StateBudget, MaxAttempts).Seed;

        /// <summary>
        /// Convenience overload that uses the pinned Klondike draw-1 rule.
        /// Callers that cannot access <see cref="IDealRuleFactory"/> (e.g. Lobby) use this.
        /// </summary>
        public static int ResolveForKlondikeDrawOne(DateTime utcDate)
            => ResolveFor(utcDate, GameType.Klondike, FrozenKlondikeDrawOneRule.Instance);

        /// <summary>
        /// Async variant of <see cref="ResolveForKlondikeDrawOne"/>. Contract-equal: uses the
        /// same pinned constants (StateBudget/MaxAttempts) and the same frozen Klondike draw-1
        /// rule, so async and sync entries always resolve to the same seed for a given date.
        /// Time-sliced on the PlayerLoop — safe on WebGL where thread-pool tasks never complete.
        /// </summary>
        public static async UniTask<int> ResolveForKlondikeDrawOneAsync(
            DateTime utcDate, CancellationToken ct = default)
        {
            var resolved = await SolvableSeedResolver.ResolveAsync(
                DailySeed.For(utcDate, GameType.Klondike),
                FrozenKlondikeDrawOneRule.Instance,
                StateBudget,
                MaxAttempts,
                ct);
            return resolved.Seed;
        }

        /// <summary>
        /// Exposes the frozen Klondike draw-1 rule for the contract-link test in
        /// DailySeedResolverV2Tests. Not intended for general use — prefer
        /// <see cref="ResolveForKlondikeDrawOne"/> which already uses this rule internally.
        /// </summary>
        public static IDealRule FrozenKlondikeDrawOneRuleInstance => FrozenKlondikeDrawOneRule.Instance;

        // Frozen Klondike draw-1 rule — identical to a DealRuleAsset with default settings.
        // Pinned here so the lobby and tests don't need the DI-bound DealRuleFactory.
        private sealed class FrozenKlondikeDrawOneRule : IDealRule
        {
            public static readonly FrozenKlondikeDrawOneRule Instance = new();
            private FrozenKlondikeDrawOneRule() { }

            private static readonly int[] initialCardCounts = { 1, 2, 3, 4, 5, 6, 7 };

            public int TableauCount => 7;
            public int FoundationCount => 4;
            public int PerSuitCardCount => 13;
            public bool HasWaste => true;
            public bool CanRecycleStock => true;
            public int StockDrawCount => 1;
            public bool StockDealsToTableau => false;
            public int[] InitialCardCounts => initialCardCounts;
            public int InitialFaceUpPerColumn => 1;
            public bool OnlyKingOnEmptyTableau => true;
        }
    }
}
