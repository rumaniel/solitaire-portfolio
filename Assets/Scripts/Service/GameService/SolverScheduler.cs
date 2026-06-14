using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Model.Board;
using Model.Game;

namespace Service.GameService
{
    /// <summary>Runs seed resolution off the main thread where threads exist, and as
    /// PlayerLoop time slices on WebGL (managed threads never run there — a thread-pool
    /// task would hang forever, not just stall).</summary>
    public static class SolverScheduler
    {
        /// <summary>Resolves a Klondike/Easthaven seed using rule-derived budget defaults.</summary>
        public static UniTask<SolvableSeedResolver.ResolveResult> ResolveAsync(
            int inputSeed, IDealRule rule, CancellationToken ct = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return SolvableSeedResolver.ResolveAsync(inputSeed, rule, ct);
#else
            return UniTask.RunOnThreadPool(
                () => SolvableSeedResolver.Resolve(inputSeed, rule, ct),
                cancellationToken: ct);
#endif
        }

        /// <summary>Resolves a Pyramid or TriPeaks seed.</summary>
        public static UniTask<BoardSolvableSeedResolver.ResolveResult> ResolveBoardAsync(
            int inputSeed, GameType gameType, BoardLayout layout, CancellationToken ct = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return BoardSolvableSeedResolver.ResolveAsync(inputSeed, gameType, layout, ct);
#else
            return UniTask.RunOnThreadPool(
                () => BoardSolvableSeedResolver.Resolve(inputSeed, gameType, layout, ct),
                cancellationToken: ct);
#endif
        }

        /// <summary>Resolves the daily Klondike draw-1 seed for the given UTC date.</summary>
        public static UniTask<int> ResolveDailyKlondikeDrawOneAsync(
            DateTime utcDate, CancellationToken ct = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return DailySeedResolverV2.ResolveForKlondikeDrawOneAsync(utcDate, ct);
#else
            return UniTask.RunOnThreadPool(
                () => DailySeedResolverV2.ResolveForKlondikeDrawOne(utcDate),
                cancellationToken: ct);
#endif
        }
    }
}
