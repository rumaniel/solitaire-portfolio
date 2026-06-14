using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Model.Board;
using Model.Card;
using Model.Game;
using Service.BoardGameService;

namespace Service.GameService
{
    /// <summary>
    /// Resolves an input seed to a proven-winnable board deal seed (Pyramid/TriPeaks).
    /// Same resolve-then-share contract as <see cref="SolvableSeedResolver"/>: callers
    /// persist/share the resolved seed (GameCode) so replays are reproducible.
    ///
    /// Budgets from benchmarks:
    /// <list type="bullet">
    ///   <item>TriPeaks: 50k states / 3 attempts (~85% proven per attempt at 50k).</item>
    ///   <item>Pyramid:  200k states / 4 attempts (~45% proven at 50k; deeper budget
    ///     is cheap at ~1 µs/state so 200k is affordable).</item>
    /// </list>
    /// </summary>
    public static class BoardSolvableSeedResolver
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

        /// <summary>Resolves an input seed to a proven-winnable board deal seed (Pyramid/TriPeaks). Same resolve-then-share contract as SolvableSeedResolver.</summary>
        /// <param name="inputSeed">Caller-supplied seed (e.g. from <see cref="DeckFactory.CreateRandomSeed"/>).</param>
        /// <param name="gameType">Must be <see cref="GameType.TriPeaks"/> or <see cref="GameType.Pyramid"/>.</param>
        /// <param name="layout">Board topology; must match the game type.</param>
        /// <param name="ct">Optional cancellation token.</param>
        /// <returns>A deterministic result with the resolved seed and whether solvability was proven.</returns>
        public static ResolveResult Resolve(
            int inputSeed,
            GameType gameType,
            BoardLayout layout,
            CancellationToken ct = default)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (layout.GameType != gameType)
                throw new ArgumentException(
                    $"Layout GameType ({layout.GameType}) does not match requested gameType ({gameType}).",
                    nameof(layout));

            int stateBudget;
            int maxAttempts;
            switch (gameType)
            {
                case GameType.TriPeaks:
                    stateBudget = 50_000;
                    maxAttempts = 3;
                    break;
                case GameType.Pyramid:
                    stateBudget = 200_000;
                    maxAttempts = 4;
                    break;
                default:
                    throw new ArgumentException(
                        $"BoardSolvableSeedResolver does not support game type {gameType}. " +
                        "Supported: TriPeaks, Pyramid.",
                        nameof(gameType));
            }

            int candidate = 0;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                candidate = SeedMix.Mix(inputSeed, attempt);
                var state = BuildDeal(candidate, gameType, layout);
                bool solved = gameType == GameType.TriPeaks
                    ? TriPeaksSolver.Solve(state, layout, stateBudget, ct).Solved
                    : PyramidSolver.Solve(state, layout, maxRecycles: 3, stateBudget: stateBudget, ct: ct).Solved;

                if (solved)
                    return new ResolveResult(candidate, attempt + 1, proven: true);
            }

            return new ResolveResult(candidate, maxAttempts, proven: false);
        }

        /// <summary>
        /// Async variant. Contract-equal to <see cref="Resolve"/>: identical Mix chain,
        /// same pinned budgets/attempts, same solver verdicts. Each attempt awaits the
        /// respective time-sliced SolveAsync which is safe on WebGL.
        /// </summary>
        public static async UniTask<ResolveResult> ResolveAsync(
            int inputSeed,
            GameType gameType,
            BoardLayout layout,
            CancellationToken ct = default)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (layout.GameType != gameType)
                throw new ArgumentException(
                    $"Layout GameType ({layout.GameType}) does not match requested gameType ({gameType}).",
                    nameof(layout));

            int stateBudget;
            int maxAttempts;
            switch (gameType)
            {
                case GameType.TriPeaks:
                    stateBudget = 50_000;
                    maxAttempts = 3;
                    break;
                case GameType.Pyramid:
                    stateBudget = 200_000;
                    maxAttempts = 4;
                    break;
                default:
                    throw new ArgumentException(
                        $"BoardSolvableSeedResolver does not support game type {gameType}.",
                        nameof(gameType));
            }

            int candidate = 0;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                candidate = SeedMix.Mix(inputSeed, attempt);
                var state = BuildDeal(candidate, gameType, layout);
                bool solved = gameType == GameType.TriPeaks
                    ? (await TriPeaksSolver.SolveAsync(state, layout, stateBudget, ct)).Solved
                    : (await PyramidSolver.SolveAsync(state, layout, maxRecycles: 3, stateBudget: stateBudget, ct: ct)).Solved;

                if (solved)
                    return new ResolveResult(candidate, attempt + 1, proven: true);
            }

            return new ResolveResult(candidate, maxAttempts, proven: false);
        }

        /// <summary>
        /// Builds a <see cref="BoardState"/> that mirrors exactly what
        /// <see cref="BoardGameServiceBase.Initialize"/> + the game-specific <c>OnDealt</c> hook does:
        /// <list type="number">
        ///   <item>Shuffle with <see cref="DeckFactory.CreateShuffled"/> (Fisher-Yates) — identical to
        ///     <see cref="FisherYatesShuffleStrategy"/>, which the board scene registers as
        ///     <see cref="IShuffleStrategy"/> and <c>Initialize</c> delegates to.</item>
        ///   <item>First <c>layout.Count</c> cards → cells; remainder → stock; waste empty.</item>
        ///   <item>For TriPeaks: call <c>WithStockDrawn()</c> to mirror <c>TriPeaksGameService.OnDealt</c>,
        ///     which flips the first stock card to the waste so play has an anchor.</item>
        ///   <item>For Pyramid: no OnDealt override — state is used as-is.</item>
        /// </list>
        /// </summary>
        // Public rather than internal: the EditMode test assembly replays resolved
        // seeds through this same canonical deal path (separate asmdef).
        public static BoardState BuildDeal(int seed, GameType gameType, BoardLayout layout)
        {
            var deck = DeckFactory.CreateShuffled(seed);

            var cellCards = new List<PlayingCard>(layout.Count);
            for (int i = 0; i < layout.Count; i++)
                cellCards.Add(deck[i]);

            var stock = new List<PlayingCard>(deck.Count - layout.Count);
            for (int i = layout.Count; i < deck.Count; i++)
                stock.Add(deck[i]);

            var state = new BoardState(cellCards, stock, waste: null);

            // Mirror TriPeaksGameService.OnDealt: flip the first stock card to the waste.
            if (gameType == GameType.TriPeaks && state.Stock.Count > 0)
                state = state.WithStockDrawn();

            return state;
        }
    }
}
