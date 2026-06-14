using System;
using Cysharp.Threading.Tasks;
using Model.Stats;
using R3;

namespace Service.StatsService
{
    public interface IDailyStatsService
    {
        DailyStats Stats { get; }
        Observable<DailyStats> OnStatsChanged { get; }
        bool IsCompletedToday(DateTime utcNow);
        bool IsAttemptedToday(DateTime utcNow);
        DailyRecord GetTodayRecord(DateTime utcNow);
        UniTask RecordResultAsync(DateTime utcNow, bool won, SessionStats session);
        UniTask LoadAsync();
    }
}
