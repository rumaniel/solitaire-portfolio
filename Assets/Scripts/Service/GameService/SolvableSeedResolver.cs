using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Model.Game;

namespace Service.GameService
{
    /// <summary>
    /// Resolves an input seed to a Fisher-Yates deal seed whose Klondike deal is
    /// proven solvable by <see cref="KlondikeFastSolver"/>, by deterministically
    /// resampling candidates until one passes. Callers persist/share the resolved
    /// seed (GameCode), so replays never need the solver and stay reproducible
    /// across app versions regardless of solver changes.
    /// </summary>
    public static class SolvableSeedResolver
    {
        public readonly struct ResolveResult
        {
            public readonly int Seed;
            public readonly int Attempts;
            public readonly bool Proven;

            public ResolveResult(int seed, int attempts, bool proven)
            {
                Seed = seed;
                Attempts = attempts;
                Proven = proven;
            }
        }

        /// <summary>
        /// Resolves with budget/attempts tuned from benchmarks. Rule-aware selection:
        /// <list type="bullet">
        ///   <item>Easthaven (<c>StockDealsToTableau</c>): 50k states / 4 attempts.
        ///     ~30% proven per attempt at 50k states; ~350ms worst-case per attempt.
        ///     Unproven fallback after 4 attempts is deliberate policy.</item>
        ///   <item>Draw-3 Klondike: 50k states / 4 attempts (~60% proven, ~0.5s worst).</item>
        ///   <item>Draw-1 Klondike: 5k states / 8 attempts (~65% proven, ~10ms worst).</item>
        /// </list>
        /// Attempt caps bound worst-case latency; an unproven deal is still played.
        /// </summary>
        public static ResolveResult Resolve(int inputSeed, IDealRule rule, CancellationToken ct = default)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            int budget;
            int attempts;
            if (rule.StockDealsToTableau)
            {
                // Easthaven: stock deals 3 cards face-down to each tableau column;
                // solvability is harder to prove so we budget more states.
                budget = 50_000;
                attempts = 4;
            }
            else if (rule.StockDrawCount >= 3)
            {
                budget = 50_000;
                attempts = 4;
            }
            else
            {
                budget = 5_000;
                attempts = 8;
            }
            return Resolve(inputSeed, rule, budget, attempts, ct);
        }

        /// <summary>
        /// Async variant using rule-derived defaults. Contract-equal to <see cref="Resolve(int,IDealRule,CancellationToken)"/>:
        /// same Mix chain, same solver verdicts, same budget/attempt constants.
        /// </summary>
        public static async UniTask<ResolveResult> ResolveAsync(int inputSeed, IDealRule rule, CancellationToken ct = default)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            int budget;
            int attempts;
            if (rule.StockDealsToTableau)
            {
                budget = 50_000;
                attempts = 4;
            }
            else if (rule.StockDrawCount >= 3)
            {
                budget = 50_000;
                attempts = 4;
            }
            else
            {
                budget = 5_000;
                attempts = 8;
            }
            return await ResolveAsync(inputSeed, rule, budget, attempts, ct);
        }

        /// <summary>
        /// Async variant with explicit budget/attempts. Contract-equal to
        /// <see cref="Resolve(int,IDealRule,int,int,CancellationToken)"/>: identical Mix chain
        /// and solver verdicts — each attempt awaits <see cref="KlondikeFastSolver.SolveAsync"/>
        /// which is time-sliced and safe on WebGL.
        /// </summary>
        public static async UniTask<ResolveResult> ResolveAsync(
            int inputSeed, IDealRule rule, int stateBudget, int maxAttempts, CancellationToken ct = default)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (stateBudget < 1) throw new ArgumentOutOfRangeException(nameof(stateBudget), "Must be >= 1.");
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Must be >= 1.");

            int candidate = 0;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                candidate = SeedMix.Mix(inputSeed, attempt);
                var deck = DeckFactory.CreateShuffled(candidate);
                var state = DealBuilder.Build(deck, rule);
                var result = await KlondikeFastSolver.SolveAsync(state, rule, stateBudget, ct);
                if (result.Solved)
                    return new ResolveResult(candidate, attempt + 1, proven: true);
            }

            return new ResolveResult(candidate, maxAttempts, proven: false);
        }

        /// <summary>
        /// Walks the deterministic candidate chain Mix(inputSeed, attempt) and returns
        /// the first candidate whose deal the solver proves solvable. If no candidate
        /// is proven within maxAttempts, returns the last candidate with Proven=false.
        /// </summary>
        public static ResolveResult Resolve(
            int inputSeed, IDealRule rule, int stateBudget, int maxAttempts, CancellationToken ct = default)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (stateBudget < 1) throw new ArgumentOutOfRangeException(nameof(stateBudget), "Must be >= 1.");
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Must be >= 1.");

            int candidate = 0;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                candidate = SeedMix.Mix(inputSeed, attempt);
                var deck = DeckFactory.CreateShuffled(candidate);
                var state = DealBuilder.Build(deck, rule);
                var result = KlondikeFastSolver.Solve(state, rule, stateBudget, ct);
                if (result.Solved)
                    return new ResolveResult(candidate, attempt + 1, proven: true);
            }

            return new ResolveResult(candidate, maxAttempts, proven: false);
        }
    }

    /// <summary>
    /// Shared splitmix64-style seed mixer used by both Klondike and board-game seed resolvers.
    /// Keeps the mixing constants in one place to prevent copy-paste drift.
    /// </summary>
    internal static class SeedMix
    {
        // splitmix64-style: consecutive attempts (and consecutive input seeds) land on
        // unrelated candidates — a plain seed+attempt offset would produce overlapping chains.
        internal static int Mix(int inputSeed, int attempt)
        {
            unchecked
            {
                ulong z = (ulong)(uint)inputSeed + (0x9E3779B97F4A7C15UL * (ulong)(uint)(attempt + 1));
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                return (int)z;
            }
        }
    }
}
