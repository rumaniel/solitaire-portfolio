using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Gateway.Achievement;
using Model.Achievement;
using Model.Game;
using Model.Stats;
using R3;
using Service.AchievementService.Evaluator;
using Service.StatsService;
using UnityEngine;

namespace Service.AchievementService
{
    /// <summary>Coordinator: owns the status cache, dispatches to the right evaluator, fans out unlock events.</summary>
    public class AchievementService : IAchievementService, IDisposable
    {
        private readonly IAchievementCatalog catalog;
        private readonly IAchievementGateway gateway;
        private readonly ILifetimeStatsService lifetimeStats;
        private readonly IDailyStatsService dailyStats;

        private readonly List<IAchievementRuleEvaluator> evaluators;
        private readonly Dictionary<string, AchievementStatus> statusById = new();
        private readonly Subject<AchievementUnlockedEvent> unlockSubject = new();
        private readonly Subject<Unit> progressSubject = new();
        private readonly CompositeDisposable disposables = new();
        // Serializes achievements.bin writes to avoid thread-pool sharing violations on bursty saves.
        private readonly SemaphoreSlim saveLock = new(1, 1);

        private bool initialized;
        private bool retroactiveSweep;
        private volatile bool disposed;

        public Observable<AchievementUnlockedEvent> OnAchievementUnlocked => unlockSubject;
        public Observable<Unit> OnProgressChanged => progressSubject;

        public AchievementService(
            IAchievementCatalog catalog,
            IAchievementGateway gateway,
            ILifetimeStatsService lifetimeStats,
            IDailyStatsService dailyStats)
        {
            this.catalog = catalog;
            this.gateway = gateway;
            this.lifetimeStats = lifetimeStats;
            this.dailyStats = dailyStats;

            evaluators = new List<IAchievementRuleEvaluator>
            {
                new LifetimeStatsRuleEvaluator(),
                new SessionAwareRuleEvaluator(),
                new DailyStatsRuleEvaluator(),
            };
        }

        public async UniTask InitializeAsync()
        {
            if (initialized || disposed) return;
            initialized = true; // guard reentrancy on first await; reset in catch to allow retry

            try
            {
                var store = await gateway.LoadAsync() ?? new AchievementStore();
                // Defensive: MemoryPack can produce a null list from a corrupted/older .bin.
                if (store.Entries != null)
                {
                    foreach (var entry in store.Entries)
                    {
                        if (entry != null && !string.IsNullOrEmpty(entry.Id))
                            statusById[entry.Id] = entry;
                    }
                }

                foreach (var def in catalog.Definitions)
                {
                    if (!statusById.ContainsKey(def.Id))
                        statusById[def.Id] = new AchievementStatus { Id = def.Id };
                }

                // Ensure DailyStats are loaded before subscribing/sweeping — otherwise the
                // retroactive sweep would see default stats and Daily achievements would later
                // unlock via LobbyPresenter's LoadAsync with Retroactive=false (unintended toast).
                // A failed Daily load here should not cascade and break app init; the sweep will
                // simply run against default Stats and Daily achievements re-evaluate on next pulse.
                if (dailyStats != null)
                {
                    try { await dailyStats.LoadAsync(); }
                    catch (Exception dailyEx)
                    {
                        Debug.LogError("[Achievement] DailyStats preload failed; retroactive sweep will use default stats.");
                        Debug.LogException(dailyEx);
                    }
                }

                SubscribeToStatsSources();

                // Retroactive sweep: emitted unlock events carry Retroactive=true so UI subscribers
                // can skip the toast and avoid back-catalog spam after app updates.
                retroactiveSweep = true;
                bool anyRetroactiveChange = false;
                foreach (GameType gt in Enum.GetValues(typeof(GameType)))
                {
                    if (gt == GameType.None) continue;
                    if (EvaluateAll(BuildContextFromCurrentStats(gt)))
                        anyRetroactiveChange = true;
                }
                retroactiveSweep = false;

                if (anyRetroactiveChange)
                    await SaveAsync();
            }
            catch (Exception e)
            {
                initialized = false;
                Debug.LogError("[Achievement] InitializeAsync failed.");
                Debug.LogException(e);
                throw;
            }
        }

        public IReadOnlyList<(IAchievementDefinition Definition, AchievementStatus Status)> GetAll()
        {
            var list = new List<(IAchievementDefinition, AchievementStatus)>(catalog.Definitions.Count);
            foreach (var def in catalog.Definitions)
            {
                var status = statusById.TryGetValue(def.Id, out var s) ? s : new AchievementStatus { Id = def.Id };
                list.Add((def, status));
            }
            return list;
        }

        public IAchievementDefinition GetDefinition(string id)
            => catalog.TryGet(id, out var def) ? def : null;

        public AchievementStatus GetStatus(string id)
            => statusById.TryGetValue(id, out var status) ? status : new AchievementStatus { Id = id };

        public void EvaluateOnGameEnd(GameType gameType, LifetimeStats lifetime, SessionStats sessionSnapshot)
        {
            if (disposed) return;
            var ctx = new EvaluationContext(gameType, lifetime, sessionSnapshot, dailyStats?.Stats);
            if (EvaluateAll(ctx))
                SaveAsync().Forget();
        }

        private void SubscribeToStatsSources()
        {
            if (lifetimeStats != null)
            {
                lifetimeStats.OnStatsChanged
                    .Subscribe(tuple => OnLifetimeStatsChanged(tuple.gameType, tuple.stats))
                    .AddTo(disposables);
            }
            if (dailyStats != null)
            {
                dailyStats.OnStatsChanged
                    .Subscribe(OnDailyStatsChanged)
                    .AddTo(disposables);
            }
        }

        private void OnLifetimeStatsChanged(GameType gameType, LifetimeStats lifetime)
        {
            if (disposed) return;
            var ctx = new EvaluationContext(gameType, lifetime, null, dailyStats?.Stats);
            if (EvaluateAll(ctx))
                SaveAsync().Forget();
        }

        private void OnDailyStatsChanged(DailyStats daily)
        {
            if (disposed) return;
            var ctx = new EvaluationContext(GameType.None, null, null, daily);
            if (EvaluateAll(ctx))
                SaveAsync().Forget();
        }

        /// <summary>Returns true if any status changed, so callers can skip disk writes when nothing did.</summary>
        private bool EvaluateAll(EvaluationContext ctx)
        {
            bool anyChange = false;
            foreach (var def in catalog.Definitions)
            {
                if (!ScopeMatches(def, ctx)) continue;
                if (EvaluateOne(def, ctx)) anyChange = true;
            }
            if (anyChange)
            {
                try { progressSubject.OnNext(Unit.Default); }
                catch (ObjectDisposedException) { }
            }
            return anyChange;
        }

        /// <summary>Evaluates a single definition. Returns true if any state/progress changed.</summary>
        private bool EvaluateOne(IAchievementDefinition def, EvaluationContext ctx)
        {
            if (!statusById.TryGetValue(def.Id, out var status))
            {
                status = new AchievementStatus { Id = def.Id };
                statusById[def.Id] = status;
            }

            var evaluator = FindEvaluator(def.RuleType);
            if (evaluator == null) return false;

            // Already-unlocked incremental entries only refresh their displayed progress count.
            if (status.State == AchievementState.Unlocked)
                return UpdateProgressOnly(def, status, evaluator, ctx);

            var result = evaluator.Evaluate(def, status, ctx);
            bool progressChanged = result.NewProgress != status.CurrentProgress;
            status.CurrentProgress = result.NewProgress;

            if (result.ShouldUnlock)
            {
                status.State = AchievementState.Unlocked;
                status.UnlockedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                try
                {
                    unlockSubject.OnNext(new AchievementUnlockedEvent(
                        def.Id, status.UnlockedAtUnix, retroactiveSweep));
                }
                catch (ObjectDisposedException) { }
                return true;
            }

            return progressChanged;
        }

        private static bool UpdateProgressOnly(
            IAchievementDefinition def, AchievementStatus status,
            IAchievementRuleEvaluator evaluator, EvaluationContext ctx)
        {
            if (!def.IsIncremental) return false;
            var result = evaluator.Evaluate(def, status, ctx);
            if (result.NewProgress == status.CurrentProgress) return false;
            status.CurrentProgress = result.NewProgress;
            return true;
        }

        private IAchievementRuleEvaluator FindEvaluator(AchievementRuleType type)
        {
            foreach (var e in evaluators)
                if (e.CanHandle(type)) return e;
            return null;
        }

        private static bool ScopeMatches(IAchievementDefinition def, EvaluationContext ctx)
        {
            // None acts as wildcard on either side: scope-None defs always apply; ctx-None pulses
            // (e.g. daily stats changes) match every scoped def.
            if (def.ScopeGameType == GameType.None) return true;
            if (ctx.GameType == GameType.None) return true;
            return def.ScopeGameType == ctx.GameType;
        }

        private EvaluationContext BuildContextFromCurrentStats(GameType gameType)
        {
            var lifetime = lifetimeStats?.GetStats(gameType);
            return new EvaluationContext(gameType, lifetime, null, dailyStats?.Stats);
        }

        private async UniTask SaveAsync()
        {
            if (disposed) return;

            try { await saveLock.WaitAsync(); }
            catch (ObjectDisposedException) { return; }

            try
            {
                if (disposed) return;

                // Snapshot inside the lock; gateway.SaveAsync serializes synchronously before its
                // file-write yield, so the bytes are frozen before any later mutation.
                var store = new AchievementStore();
                foreach (var status in statusById.Values)
                    store.Entries.Add(status);

                await gateway.SaveAsync(store);
            }
            catch (Exception e)
            {
                Debug.LogError("[Achievement] Save failed.");
                Debug.LogException(e);
            }
            finally
            {
                try { saveLock.Release(); }
                catch (ObjectDisposedException) { }
            }
        }

        public void Dispose()
        {
            disposed = true;
            disposables.Dispose();
            unlockSubject.Dispose();
            progressSubject.Dispose();
            saveLock.Dispose();
        }
    }
}
