using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Gateway.Stats;
using Model.Game;
using Model.Stats;
using R3;

namespace Service.StatsService
{
    public class LifetimeStatsService : ILifetimeStatsService, IDisposable
    {
        private readonly IStatsRepository repository;
        private readonly Dictionary<GameType, LifetimeStats> cache = new();
        private readonly Subject<(GameType, LifetimeStats)> statsSubject = new();
        private readonly SemaphoreSlim semaphore = new(1, 1);
        private volatile bool disposed;

        public Observable<(GameType gameType, LifetimeStats stats)> OnStatsChanged => statsSubject;

        public LifetimeStatsService(IStatsRepository repository)
        {
            this.repository = repository;
        }

        public async UniTask InitializeAsync()
        {
            if (disposed) return;
            try
            {
                await semaphore.WaitAsync();
            }
            catch (ObjectDisposedException) { return; }

            try
            {
                foreach (GameType gt in Enum.GetValues(typeof(GameType)))
                {
                    if (gt == GameType.None) continue;
                    var stats = await repository.LoadAsync(gt);
                    cache[gt] = stats ?? new LifetimeStats();
                }
            }
            finally
            {
                ReleaseSafe();
            }
        }

        public LifetimeStats GetStats(GameType gameType)
        {
            return cache.TryGetValue(gameType, out var stats) ? stats : new LifetimeStats();
        }

        public async UniTask RecordGameResultAsync(GameType gameType, SessionStats session)
        {
            if (gameType == GameType.None || disposed) return;

            try
            {
                await semaphore.WaitAsync();
            }
            catch (ObjectDisposedException) { return; }

            try
            {
                if (!cache.TryGetValue(gameType, out var stats))
                {
                    stats = new LifetimeStats();
                    cache[gameType] = stats;
                }

                stats.TotalGamesPlayed++;
                stats.TotalScore += session.Score;

                if (session.IsWon)
                {
                    stats.TotalGamesWon++;
                    stats.CurrentWinStreak++;
                    if (stats.CurrentWinStreak > stats.BestWinStreak)
                        stats.BestWinStreak = stats.CurrentWinStreak;

                    if (session.ElapsedSeconds < stats.ShortestWinTime)
                        stats.ShortestWinTime = session.ElapsedSeconds;
                    if (session.ElapsedSeconds > stats.LongestWinTime)
                        stats.LongestWinTime = session.ElapsedSeconds;
                    stats.TotalWinTime += session.ElapsedSeconds;

                    if (session.MoveCount < stats.MinWinMoves)
                        stats.MinWinMoves = session.MoveCount;
                    if (session.MoveCount > stats.MaxWinMoves)
                        stats.MaxWinMoves = session.MoveCount;
                    stats.TotalWinMoves += session.MoveCount;

                    if (!session.UndoUsed)
                        stats.GamesWonWithoutUndo++;
                    if (!session.HintUsed)
                        stats.GamesWonWithoutHints++;

                    if (session.Score > stats.HighScore)
                        stats.HighScore = session.Score;
                }
                else
                {
                    stats.TotalGamesLost++;
                    stats.CurrentWinStreak = 0;
                }

                // Best-effort persistence: a write failure is contained here (one bounded
                // boundary) so it cannot abort the in-memory update or the presenter's
                // end-game flow (achievement evaluation, win UI).
                try { await repository.SaveAsync(gameType, stats); }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[LifetimeStats] Persist failed for {gameType}.");
                    UnityEngine.Debug.LogException(e);
                }
                try { statsSubject.OnNext((gameType, stats)); }
                catch (ObjectDisposedException) { }
            }
            finally
            {
                ReleaseSafe();
            }
        }

        public void Dispose()
        {
            disposed = true;
            statsSubject.Dispose();
            semaphore.Dispose();
        }

        private void ReleaseSafe()
        {
            try { semaphore.Release(); }
            catch (ObjectDisposedException) { }
        }
    }
}
