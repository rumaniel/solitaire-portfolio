using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Model.Card;
using Model.Game;
using Model.Stats;
using Service.GameService;
using Service.StatsService;

namespace Tests.EditMode
{
    [TestFixture]
    public class GameSnapshotTests
    {
        private SolitaireGameService _gameService;
        private StubDealRule _rule;
        private const int TestSeed = 42;

        [SetUp]
        public void SetUp()
        {
            _rule = new StubDealRule();
            _gameService = new SolitaireGameService(new ShuffleStrategyProvider());
            _gameService.Initialize(_rule, TestSeed);
        }

        [TearDown]
        public void TearDown()
        {
            _gameService.Dispose();
        }

        // --- UndoHistory ---

        [Test]
        public void UndoHistory_AfterInitialize_IsEmpty()
        {
            Assert.AreEqual(0, _gameService.UndoHistory.Count);
        }

        [Test]
        public void UndoHistory_AfterDrawFromStock_HasOneEntry()
        {
            _gameService.DrawFromStock();
            Assert.AreEqual(1, _gameService.UndoHistory.Count);
        }

        [Test]
        public void UndoHistory_EnumerationOrder_IsLifo()
        {
            // Draw twice to create two history entries
            _gameService.DrawFromStock();
            var stateAfterFirstDraw = _gameService.CurrentState;
            _gameService.DrawFromStock();

            // UndoHistory should enumerate most-recent-first (LIFO)
            var history = _gameService.UndoHistory.ToArray();
            Assert.AreEqual(2, history.Length);
            // First element should be the state before the second draw (= stateAfterFirstDraw)
            Assert.AreSame(stateAfterFirstDraw, history[0]);
        }

        // --- Restore round-trip ---

        [Test]
        public void Restore_PreservesCurrentState()
        {
            _gameService.DrawFromStock();
            var originalState = _gameService.CurrentState;
            var originalHistory = _gameService.UndoHistory.ToList();

            // Restore into a fresh service
            var restored = new SolitaireGameService(new ShuffleStrategyProvider());
            restored.Restore(_rule, TestSeed, originalState, originalHistory);

            Assert.AreSame(originalState, restored.CurrentState);
            Assert.AreEqual(TestSeed, restored.CurrentSeed);
            restored.Dispose();
        }

        [Test]
        public void Restore_PreservesUndoHistory_Order()
        {
            // Build up undo history: draw 3 times
            _gameService.DrawFromStock();
            _gameService.DrawFromStock();
            _gameService.DrawFromStock();

            var originalHistory = _gameService.UndoHistory.ToList();
            Assert.AreEqual(3, originalHistory.Count);

            // Restore
            var restored = new SolitaireGameService(new ShuffleStrategyProvider());
            restored.Restore(_rule, TestSeed, _gameService.CurrentState, originalHistory);

            // Undo should produce the same sequence of states
            var restoredHistory = restored.UndoHistory.ToList();
            Assert.AreEqual(originalHistory.Count, restoredHistory.Count);
            for (int i = 0; i < originalHistory.Count; i++)
                Assert.AreSame(originalHistory[i], restoredHistory[i]);

            restored.Dispose();
        }

        [Test]
        public void Restore_Undo_RestoresMostRecentState()
        {
            _gameService.DrawFromStock();
            var stateBeforeSecondDraw = _gameService.CurrentState;
            _gameService.DrawFromStock();

            var history = _gameService.UndoHistory.ToList();

            var restored = new SolitaireGameService(new ShuffleStrategyProvider());
            restored.Restore(_rule, TestSeed, _gameService.CurrentState, history);

            Assert.IsTrue(restored.CanUndo);
            restored.Undo();
            // After undo, should be back to the state before the second draw
            Assert.AreSame(stateBeforeSecondDraw, restored.CurrentState);

            restored.Dispose();
        }

        [Test]
        public void Restore_MultipleUndos_RestoresCorrectSequence()
        {
            var stateAfterInit = _gameService.CurrentState;
            _gameService.DrawFromStock();
            var stateAfterFirstDraw = _gameService.CurrentState;
            _gameService.DrawFromStock();

            var history = _gameService.UndoHistory.ToList();

            var restored = new SolitaireGameService(new ShuffleStrategyProvider());
            restored.Restore(_rule, TestSeed, _gameService.CurrentState, history);

            restored.Undo();
            Assert.AreSame(stateAfterFirstDraw, restored.CurrentState);

            restored.Undo();
            Assert.AreSame(stateAfterInit, restored.CurrentState);

            Assert.IsFalse(restored.CanUndo);

            restored.Dispose();
        }

        // --- GameSnapshotConverter round-trip ---

        [Test]
        public void Converter_RoundTrip_PreservesTableState()
        {
            _gameService.DrawFromStock();
            var original = _gameService.CurrentState;

            var dto = GameSnapshotConverter.ToSnapshot(
                GameType.Klondike, TestSeed, 1, original, _gameService.UndoHistory,
                new SessionStats());
            var restored = GameSnapshotConverter.ToTableState(dto.CurrentState);

            // Verify all piles match
            AssertPileEqual(original.Stock, restored.Stock);
            AssertPileEqual(original.Waste, restored.Waste);
            Assert.AreEqual(original.WasteFanCount, restored.WasteFanCount);
            Assert.AreEqual(original.Foundations.Count, restored.Foundations.Count);
            for (int i = 0; i < original.Foundations.Count; i++)
                AssertPileEqual(original.Foundations[i], restored.Foundations[i]);
            Assert.AreEqual(original.Tableaus.Count, restored.Tableaus.Count);
            for (int i = 0; i < original.Tableaus.Count; i++)
                AssertPileEqual(original.Tableaus[i], restored.Tableaus[i]);
        }

        [Test]
        public void Converter_RoundTrip_PreservesUndoHistory()
        {
            _gameService.DrawFromStock();
            _gameService.DrawFromStock();
            var originalHistory = _gameService.UndoHistory;

            var dto = GameSnapshotConverter.ToSnapshot(
                GameType.Klondike, TestSeed, 1, _gameService.CurrentState, originalHistory,
                new SessionStats());
            var restoredHistory = GameSnapshotConverter.ToHistory(dto.UndoHistory);

            Assert.AreEqual(originalHistory.Count, restoredHistory.Count);
        }

        [Test]
        public void Converter_RoundTrip_PreservesSessionStats()
        {
            var stats = new SessionStats
            {
                Score = 150,
                MoveCount = 42,
                ElapsedSeconds = 123.5f,
                UndoUsed = true,
                HintUsed = true,
                HintCount = 3
            };

            var dto = GameSnapshotConverter.ToSnapshot(
                GameType.Klondike, TestSeed, 1, _gameService.CurrentState,
                _gameService.UndoHistory, stats);
            var restored = GameSnapshotConverter.ToSessionStats(dto.Stats);

            Assert.AreEqual(stats.Score, restored.Score);
            Assert.AreEqual(stats.MoveCount, restored.MoveCount);
            Assert.AreEqual(stats.ElapsedSeconds, restored.ElapsedSeconds, 0.01f);
            Assert.AreEqual(stats.UndoUsed, restored.UndoUsed);
            Assert.AreEqual(stats.HintUsed, restored.HintUsed);
            Assert.AreEqual(stats.HintCount, restored.HintCount);
        }

        [Test]
        public void Converter_RoundTrip_PreservesGameMetadata()
        {
            var dto = GameSnapshotConverter.ToSnapshot(
                GameType.Klondike, TestSeed, 3, _gameService.CurrentState,
                _gameService.UndoHistory, new SessionStats());

            Assert.AreEqual(GameType.Klondike, dto.GameType);
            Assert.AreEqual(TestSeed, dto.Seed);
            Assert.AreEqual(3, dto.DrawCount);
            Assert.Greater(dto.SavedAtUtcTicks, 0);
        }

        // --- SessionStatsService.Restore ---

        [Test]
        public void SessionStatsService_Restore_PreservesStats()
        {
            var rule = new StubScoreRule();
            var service = new SessionStatsService();
            service.Initialize(rule);

            // Build up some stats
            service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Tableau));
            service.Tick(10f);

            var stats = new SessionStats
            {
                Score = 99,
                MoveCount = 7,
                ElapsedSeconds = 55.5f,
                UndoUsed = true,
                HintUsed = true,
                HintCount = 2
            };

            service.Restore(rule, stats);

            Assert.AreEqual(99, service.Current.Score);
            Assert.AreEqual(7, service.Current.MoveCount);
            Assert.AreEqual(55.5f, service.Current.ElapsedSeconds, 0.01f);
            Assert.IsTrue(service.Current.UndoUsed);
            Assert.IsTrue(service.Current.HintUsed);
            Assert.AreEqual(2, service.Current.HintCount);
            Assert.IsFalse(service.Current.IsFinished);
            Assert.IsFalse(service.IsPaused);

            service.Dispose();
        }

        [Test]
        public void SessionStatsService_Restore_AllowsTickingFromRestoredTime()
        {
            var rule = new StubScoreRule();
            var service = new SessionStatsService();
            service.Initialize(rule);

            var stats = new SessionStats { ElapsedSeconds = 100f };
            service.Restore(rule, stats);

            service.Tick(0.5f);
            Assert.AreEqual(100.5f, service.Current.ElapsedSeconds, 0.01f);

            service.Dispose();
        }

        [Test]
        public void SessionStatsService_Restore_AllowsScoringAfterRestore()
        {
            var rule = new StubScoreRule();
            var service = new SessionStatsService();
            service.Initialize(rule);

            var stats = new SessionStats { Score = 50, MoveCount = 10 };
            service.Restore(rule, stats);

            service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Tableau));
            Assert.AreEqual(50 + rule.WasteToTableau, service.Current.Score);
            Assert.AreEqual(11, service.Current.MoveCount);

            service.Dispose();
        }

        private static void AssertPileEqual(PileState expected, PileState actual)
        {
            Assert.AreEqual(expected.Id.Type, actual.Id.Type);
            Assert.AreEqual(expected.Id.Index, actual.Id.Index);
            Assert.AreEqual(expected.FaceUpFromIndex, actual.FaceUpFromIndex);
            Assert.AreEqual(expected.Cards.Count, actual.Cards.Count);
            for (int i = 0; i < expected.Cards.Count; i++)
            {
                Assert.AreEqual(expected.Cards[i].Rank, actual.Cards[i].Rank);
                Assert.AreEqual(expected.Cards[i].Suit, actual.Cards[i].Suit);
            }
        }
    }
}
