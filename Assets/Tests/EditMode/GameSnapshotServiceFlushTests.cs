using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Gateway.Snapshot;
using Model.Game;
using Model.Stats;
using NUnit.Framework;
using Service.CardService;
using Service.GameService;
using Service.StatsService;

namespace Tests.EditMode
{
    /// <summary>
    /// Regression tests for the FlushAndStopAsync capture-before-await contract:
    /// the snapshot must record the stats that existed at call time, not whatever
    /// the shared SessionStats holds after the IO lock is finally acquired.
    /// </summary>
    [TestFixture]
    public class GameSnapshotServiceFlushTests
    {
        // ---- stubs ----

        /// <summary>
        /// Repository whose SaveAsync blocks on <see cref="Gate"/> before recording the snapshot.
        /// Lets the test own the IO-lock timing so the "new owner resets stats" window is open.
        /// </summary>
        private sealed class GatedRepository : IGameSnapshotRepository
        {
            public ManualResetEventSlim Gate { get; } = new ManualResetEventSlim(false);
            public ManualResetEventSlim EnteredSave { get; } = new ManualResetEventSlim(false);
            public List<GameSnapshot> Saved { get; } = new List<GameSnapshot>();

            public UniTask<GameSnapshot> LoadAsync(SnapshotKey key) =>
                UniTask.FromResult<GameSnapshot>(null);

            public UniTask SaveAsync(SnapshotKey key, GameSnapshot snapshot)
            {
                EnteredSave.Set(); // proves the IO lock is held before the test proceeds
                Gate.Wait(); // hold until the test opens the gate
                Saved.Add(snapshot);
                return UniTask.CompletedTask;
            }

            public UniTask DeleteAsync(SnapshotKey key) => UniTask.CompletedTask;
        }

        private sealed class StubGameService : IGameService
        {
            public IDealRule DealRule => null;
            public int? CurrentSeed => 42;
            public TableState CurrentState { get; set; }
            public R3.Observable<TableState> OnTableStateChanged =>
                R3.Observable.Empty<TableState>();
            public IReadOnlyCollection<TableState> UndoHistory =>
                Array.Empty<TableState>();
            public bool CanUndo => false;
            public bool CanDealStock => true;
            public void Initialize(IDealRule dealRule, int? seed = null) { }
            public MoveCardResult ExecuteMove(MoveCardRequest request) => default;
            public void DrawFromStock() { }
            public bool IsWon(TableState state) => false;
            public void Undo() { }
            public void Restore(IDealRule dealRule, int seed, TableState state,
                IReadOnlyList<TableState> undoHistory) { }
        }

        // ---- helpers ----

        private static Service.SnapshotService.GameSnapshotService CreateService(
            IGameSnapshotRepository repo)
        {
            var svc = new Service.SnapshotService.GameSnapshotService();
            // Repository is [Inject] private — reflection is acceptable in tests.
            var prop = typeof(Service.SnapshotService.GameSnapshotService)
                .GetProperty("Repository",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(prop,
                "Reflection target 'Repository' not found; update this test if the property was renamed.");
            prop.SetValue(svc, repo);
            return svc;
        }

        private static TableState BuildInitialState()
        {
            var rule = new StubDealRule();
            var gs = new SolitaireGameService(new ShuffleStrategyProvider());
            gs.Initialize(rule, 42);
            var state = gs.CurrentState;
            gs.Dispose();
            return state;
        }

        // ---- tests ----

        /// <summary>
        /// Scenario: FlushAndStopAsync is called while the IO lock is already held by a concurrent
        /// FlushAsync. Before the queued continuation runs (lock is busy), the new owner calls
        /// SessionStats.Initialize, resetting MoveCount to 0. When the gate opens and
        /// FlushAndStopAsync's write completes, the snapshot must carry the original MoveCount (7),
        /// not the new owner's reset value (0).
        /// </summary>
        [Test]
        public void FlushAndStopAsync_CapturesStatsAtCallTime_NotAfterOwnerReset()
        {
            var repo = new GatedRepository();
            var svc = CreateService(repo);

            var statsService = new SessionStatsService();
            statsService.Initialize(new StubScoreRule());
            for (int i = 0; i < 7; i++)
                statsService.RecordMove(new ScoredMoveInfo(MoveType.StockDraw));
            Assert.AreEqual(7, statsService.Current.MoveCount, "pre-condition: 7 moves recorded");

            var gameService = new StubGameService { CurrentState = BuildInitialState() };
            var key = new SnapshotKey(GameType.Klondike, 1);
            svc.StartAutoSave(key, 42, gameService, statsService);

            // The whole scenario runs on the thread pool: EditMode NUnit blocks the main thread,
            // and any UniTask continuation captured on the Unity context would deadlock against
            // that block. Pool threads have no sync context, so continuations run inline.
            // The capture-before-first-await property under test is thread-agnostic.
            var scenario = Task.Run(async () =>
            {
                // Step 1: occupy the IO lock — FlushAsync acquires the uncontended lock
                // synchronously and blocks inside Gate.Wait(), so it must run on its OWN pool
                // thread or it would block this scenario thread before Gate.Set is reached.
                var firstFlushTask = Task.Run(() => svc.FlushAsync().AsTask());
                // Explicit signal, not a timing guess: the race is only exercised if the first
                // save provably holds the IO lock before FlushAndStopAsync is called.
                Assert.IsTrue(repo.EnteredSave.Wait(TimeSpan.FromSeconds(2)),
                    "first flush never reached SaveAsync (IO lock not held)");

                // Step 2: the releasing presenter's call — the UniTask method body runs
                // synchronously up to its first await (ioLock.WaitAsync), capturing the snapshot
                // and nulling gameService/statsService on this thread first.
                var stopTask = svc.FlushAndStopAsync().AsTask();

                // Step 3: simulate the new owner re-initializing the SHARED stats service.
                // Under the old implementation this raced; with capture-then-detach it is too
                // late to affect the already-captured snapshot.
                statsService.Initialize(new StubScoreRule()); // resets MoveCount to 0

                // Step 4: release the gate — both saves complete.
                repo.Gate.Set();
                await firstFlushTask;
                await stopTask;
            });

            Assert.IsTrue(scenario.Wait(TimeSpan.FromSeconds(5)), "scenario timed out");
            Assert.AreEqual(0, statsService.Current.MoveCount, "sanity: stats were reset by new owner");

            // The snapshot written by FlushAndStopAsync is the last entry (first came from FlushAsync).
            Assert.GreaterOrEqual(repo.Saved.Count, 1, "expected at least one saved snapshot");
            var stopSnapshot = repo.Saved[repo.Saved.Count - 1];

            Assert.AreEqual(7, stopSnapshot.Stats.MoveCount,
                "FlushAndStopAsync must capture stats synchronously at call time, " +
                "not after the new owner reset MoveCount to 0.");

            svc.Dispose();
            statsService.Dispose();
        }
    }
}
