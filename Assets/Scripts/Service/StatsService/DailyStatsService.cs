using System;
using Cysharp.Threading.Tasks;
using Gateway.Stats;
using Model.App;
using Model.Stats;
using R3;
using Service.GameService;

namespace Service.StatsService
{
    public class DailyStatsService : IDailyStatsService
    {
        private readonly IDailyStatsRepository repository;
        private readonly IAppConfig appConfig;
        private readonly Subject<DailyStats> statsSubject = new();

        private bool isLoaded;
        private UniTaskCompletionSource loadCompletion;

        public DailyStats Stats { get; private set; } = new DailyStats();
        public Observable<DailyStats> OnStatsChanged => statsSubject;

        public DailyStatsService(IDailyStatsRepository repository, IAppConfig appConfig)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
        }

        public async UniTask LoadAsync()
        {
            if (isLoaded) return;
            if (loadCompletion != null)
            {
                await loadCompletion.Task;
                return;
            }

            loadCompletion = new UniTaskCompletionSource();
            try
            {
                var loaded = await repository.LoadAsync();
                Stats = loaded ?? new DailyStats();
                isLoaded = true;
                statsSubject.OnNext(Stats);
                loadCompletion.TrySetResult();
            }
            catch (Exception e)
            {
                // Notify concurrent awaiters of loadCompletion.Task, then
                // re-throw to propagate to the direct caller (stacktrace preserved).
                loadCompletion.TrySetException(e);
                throw;
            }
            finally
            {
                loadCompletion = null;
            }
        }

        public bool IsCompletedToday(DateTime utcNow)
        {
            var today = DailySeed.DateKey(utcNow);
            return string.Equals(Stats.LastCompletedDateKey, today, StringComparison.Ordinal);
        }

        public bool IsAttemptedToday(DateTime utcNow)
        {
            return GetTodayRecord(utcNow) != null;
        }

        public DailyRecord GetTodayRecord(DateTime utcNow)
        {
            var today = DailySeed.DateKey(utcNow);
            if (Stats.History == null) return null;
            for (int i = Stats.History.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Stats.History[i].DateKey, today, StringComparison.Ordinal))
                    return Stats.History[i];
            }
            return null;
        }

        public async UniTask RecordResultAsync(DateTime utcNow, bool won, SessionStats session)
        {
            await LoadAsync();

            if (IsCompletedToday(utcNow))
                return;

            var today = DailySeed.DateKey(utcNow);

            // Remove stale non-win record for today (legacy data migration).
            // Decrement TotalAttempted by the same count so this day is counted once.
            Stats.History ??= new System.Collections.Generic.List<DailyRecord>();
            int removed = Stats.History.RemoveAll(r =>
                string.Equals(r.DateKey, today, StringComparison.Ordinal) && !r.Won);
            if (removed > 0)
                Stats.TotalAttempted = Math.Max(0, Stats.TotalAttempted - removed);

            Stats.History.Add(new DailyRecord
            {
                DateKey = today,
                Won = won,
                Score = session != null ? session.Score : 0,
                MoveCount = session != null ? session.MoveCount : 0,
                ElapsedSeconds = session != null ? session.ElapsedSeconds : 0f,
            });

            TrimHistory();

            Stats.TotalAttempted++;
            if (won)
            {
                Stats.TotalCompleted++;
                Stats.CurrentStreak = IsConsecutiveWithLastCompletion(utcNow)
                    ? Stats.CurrentStreak + 1
                    : 1;
                if (Stats.CurrentStreak > Stats.BestStreak)
                    Stats.BestStreak = Stats.CurrentStreak;
                Stats.LastCompletedDateKey = today;
            }
            else
            {
                Stats.CurrentStreak = 0;
            }

            statsSubject.OnNext(Stats);
            // Best-effort persistence: a write failure is contained here (one bounded
            // boundary) so it cannot propagate into the presenter's daily-win flow; the
            // in-memory update already happened.
            try { await repository.SaveAsync(Stats); }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[DailyStats] Persist failed.");
                UnityEngine.Debug.LogException(e);
            }
        }

        private bool IsConsecutiveWithLastCompletion(DateTime utcNow)
        {
            if (string.IsNullOrEmpty(Stats.LastCompletedDateKey))
                return false;
            if (!DateTime.TryParseExact(
                    Stats.LastCompletedDateKey,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var last))
                return false;
            // Integer comparison rejects negative deltas from clock rollback
            // and avoids floating-point edge cases.
            return (utcNow.Date - last.Date).Days == 1;
        }

        private void TrimHistory()
        {
            var limit = appConfig != null ? appConfig.DailyStatsHistoryLimit : 30;
            if (limit <= 0 || Stats.History == null) return;
            var overflow = Stats.History.Count - limit;
            if (overflow > 0)
                Stats.History.RemoveRange(0, overflow);
        }
    }
}
