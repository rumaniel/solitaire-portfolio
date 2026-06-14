using NUnit.Framework;
using Model.Game;
using Model.Stats;
using Service.StatsService;

namespace Tests.EditMode
{
    [TestFixture]
    public class SessionStatsServiceTests
    {
        private SessionStatsService _service;
        private StubScoreRule _rule;

        [SetUp]
        public void SetUp()
        {
            _rule = new StubScoreRule();
            _service = new SessionStatsService();
            _service.Initialize(_rule);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        // --- Initialize ---

        [Test]
        public void Initialize_ResetsAllStats()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Tableau));
            _service.Tick(5f);

            _service.Initialize(_rule);

            Assert.AreEqual(0, _service.Current.Score);
            Assert.AreEqual(0, _service.Current.MoveCount);
            Assert.AreEqual(0f, _service.Current.ElapsedSeconds);
            Assert.IsFalse(_service.Current.UndoUsed);
            Assert.IsFalse(_service.Current.IsWon);
            Assert.IsFalse(_service.Current.IsFinished);
        }

        // --- Move Counting ---

        [Test]
        public void RecordMove_IncrementsCount()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Tableau, PileType.Tableau));
            _service.RecordMove(new ScoredMoveInfo(MoveType.StockDraw));
            _service.RecordMove(new ScoredMoveInfo(MoveType.Undo));

            Assert.AreEqual(3, _service.Current.MoveCount);
        }

        // --- Score Calculation ---

        [Test]
        public void Score_WasteToTableau()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Tableau));
            Assert.AreEqual(5, _service.Current.Score);
        }

        [Test]
        public void Score_WasteToFoundation()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Foundation));
            Assert.AreEqual(10, _service.Current.Score);
        }

        [Test]
        public void Score_TableauToFoundation()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Tableau, PileType.Foundation));
            Assert.AreEqual(10, _service.Current.Score);
        }

        [Test]
        public void Score_FoundationToTableau_ClampedAtZero()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Foundation, PileType.Tableau));
            Assert.AreEqual(0, _service.Current.Score); // -15 clamped to 0
        }

        [Test]
        public void Score_FoundationToTableau_SubtractsFromExisting()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Foundation)); // +10
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Foundation)); // +10 = 20
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Foundation, PileType.Tableau)); // -15 = 5
            Assert.AreEqual(5, _service.Current.Score);
        }

        [Test]
        public void Score_TableauToTableau_NoPoints()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Tableau, PileType.Tableau));
            Assert.AreEqual(0, _service.Current.Score);
        }

        [Test]
        public void Score_TableauReveal_AddsBonus()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Tableau, PileType.Tableau, causedTableauReveal: true));
            Assert.AreEqual(5, _service.Current.Score); // 0 (T→T) + 5 (reveal)
        }

        [Test]
        public void Score_StockDraw_NoPoints()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.StockDraw));
            Assert.AreEqual(0, _service.Current.Score);
        }

        [Test]
        public void Score_StockRecycle_ClampedAtZero()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.StockRecycle));
            Assert.AreEqual(0, _service.Current.Score); // -100 clamped to 0
        }

        [Test]
        public void Score_StockRecycle_SubtractsFromExisting()
        {
            // Build up score first
            for (int i = 0; i < 12; i++)
                _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Foundation)); // +10 each = 120

            _service.RecordMove(new ScoredMoveInfo(MoveType.StockRecycle)); // -100
            Assert.AreEqual(20, _service.Current.Score);
        }

        // --- Undo ---

        [Test]
        public void Undo_SetsUndoFlag()
        {
            Assert.IsFalse(_service.Current.UndoUsed);
            _service.RecordMove(new ScoredMoveInfo(MoveType.Undo));
            Assert.IsTrue(_service.Current.UndoUsed);
        }

        [Test]
        public void Undo_RevertsLastScoreDelta()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Foundation)); // +10
            Assert.AreEqual(10, _service.Current.Score);
            _service.RecordMove(new ScoredMoveInfo(MoveType.Undo)); // reverts +10
            Assert.AreEqual(0, _service.Current.Score);
        }

        [Test]
        public void Undo_RevertsOnlyOnce()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Foundation)); // +10
            _service.RecordMove(new ScoredMoveInfo(MoveType.Undo)); // reverts +10 → 0
            _service.RecordMove(new ScoredMoveInfo(MoveType.Undo)); // no delta to revert → stays 0
            Assert.AreEqual(0, _service.Current.Score);
        }

        [Test]
        public void Undo_ScoreDoesNotGoBelowZero()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Tableau)); // +5
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Foundation)); // +10 → 15
            _service.RecordMove(new ScoredMoveInfo(MoveType.Undo)); // reverts +10 → 5
            Assert.AreEqual(5, _service.Current.Score);
        }

        // --- Timer ---

        [Test]
        public void Tick_AccumulatesTime()
        {
            _service.Tick(0.5f);
            _service.Tick(0.8f);
            Assert.AreEqual(1.3f, _service.Current.ElapsedSeconds, 0.001f);
        }

        [Test]
        public void Tick_IgnoredWhenPaused()
        {
            _service.Tick(1f);
            _service.Pause();
            _service.Tick(5f);
            Assert.AreEqual(1f, _service.Current.ElapsedSeconds, 0.001f);
        }

        [Test]
        public void Tick_ResumesAfterPause()
        {
            _service.Tick(0.5f);
            _service.Pause();
            _service.Tick(5f);
            _service.Resume();
            _service.Tick(0.7f);
            Assert.AreEqual(1.2f, _service.Current.ElapsedSeconds, 0.001f);
        }

        [Test]
        public void Tick_ClampsLargeDelta()
        {
            // Clamped to SessionStatsService.MaxTickDelta (1 second)
            _service.Tick(10f);
            Assert.AreEqual(1f, _service.Current.ElapsedSeconds, 0.001f);
        }

        // --- Freeze (Won/Lost) ---

        [Test]
        public void MarkWon_SetsIsWonAndIsFinished()
        {
            _service.MarkWon();
            Assert.IsTrue(_service.Current.IsWon);
            Assert.IsTrue(_service.Current.IsFinished);
        }

        [Test]
        public void MarkLost_SetsIsFinishedButNotIsWon()
        {
            _service.MarkLost();
            Assert.IsFalse(_service.Current.IsWon);
            Assert.IsTrue(_service.Current.IsFinished);
        }

        [Test]
        public void MarkWon_FreezesStats()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Foundation)); // +10
            _service.MarkWon();

            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Waste, PileType.Foundation));
            _service.Tick(10f);

            Assert.AreEqual(10, _service.Current.Score); // unchanged
            Assert.AreEqual(1, _service.Current.MoveCount); // unchanged
            Assert.AreEqual(0f, _service.Current.ElapsedSeconds); // unchanged
        }

        [Test]
        public void MarkLost_FreezesStats()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.StockDraw));
            _service.MarkLost();

            _service.RecordMove(new ScoredMoveInfo(MoveType.StockDraw));
            Assert.AreEqual(1, _service.Current.MoveCount);
            Assert.IsFalse(_service.Current.IsWon);
        }

        [Test]
        public void MarkWon_CannotBeCalledTwice()
        {
            _service.MarkWon();
            _service.MarkLost(); // should be ignored (already frozen)
            Assert.IsTrue(_service.Current.IsWon);
        }

        // --- Hint Tracking ---

        [Test]
        public void RecordHintUsed_SetsHintFlag()
        {
            Assert.IsFalse(_service.Current.HintUsed);
            _service.RecordHintUsed();
            Assert.IsTrue(_service.Current.HintUsed);
        }

        [Test]
        public void RecordHintUsed_IncrementsHintCount()
        {
            _service.RecordHintUsed();
            _service.RecordHintUsed();
            _service.RecordHintUsed();
            Assert.AreEqual(3, _service.Current.HintCount);
        }

        [Test]
        public void RecordHintUsed_IgnoredWhenFrozen()
        {
            _service.MarkWon();
            _service.RecordHintUsed();
            Assert.IsFalse(_service.Current.HintUsed);
            Assert.AreEqual(0, _service.Current.HintCount);
        }

        [Test]
        public void Initialize_ResetsHintStats()
        {
            _service.RecordHintUsed();
            Assert.IsTrue(_service.Current.HintUsed);

            _service.Initialize(_rule);
            Assert.IsFalse(_service.Current.HintUsed);
            Assert.AreEqual(0, _service.Current.HintCount);
        }

        // --- Pause State ---

        [Test]
        public void IsPaused_DefaultFalse()
        {
            Assert.IsFalse(_service.IsPaused);
        }

        [Test]
        public void IsPaused_TrueAfterPause()
        {
            _service.Pause();
            Assert.IsTrue(_service.IsPaused);
        }

        [Test]
        public void IsPaused_FalseAfterResume()
        {
            _service.Pause();
            _service.Resume();
            Assert.IsFalse(_service.IsPaused);
        }

        // --- RecordScoreDelta ---

        [Test]
        public void RecordScoreDelta_AddsScoreAndCountsMove()
        {
            using var stats = new SessionStatsService();
            stats.Initialize(new ZeroScoreRule());

            stats.RecordScoreDelta(10);
            stats.RecordScoreDelta(5);

            Assert.AreEqual(15, stats.Current.Score);
            Assert.AreEqual(2, stats.Current.MoveCount);
        }

        [Test]
        public void RecordScoreDelta_ThenUndo_SubtractsLastDeltaAndCountsMove()
        {
            using var stats = new SessionStatsService();
            stats.Initialize(new ZeroScoreRule());

            stats.RecordScoreDelta(10);
            stats.RecordScoreDelta(7);
            stats.RecordMove(new ScoredMoveInfo(MoveType.Undo));

            Assert.AreEqual(10, stats.Current.Score);   // last delta (7) removed
            Assert.AreEqual(3, stats.Current.MoveCount); // 2 deltas + 1 undo
            Assert.IsTrue(stats.Current.UndoUsed);
        }

        [Test]
        public void RecordScoreDelta_NeverNegative()
        {
            using var stats = new SessionStatsService();
            stats.Initialize(new ZeroScoreRule());
            stats.RecordScoreDelta(3);
            stats.RecordScoreDelta(-100);
            Assert.AreEqual(0, stats.Current.Score);
        }

        [Test]
        public void RecordScoreDelta_PenaltyFullyClampedAtZero_ThenUndo_AwardsNoPoints()
        {
            using var stats = new SessionStatsService();
            stats.Initialize(new ZeroScoreRule());

            // Score is 0; a -5 penalty (e.g. a TriPeaks stock draw) is clamped and never reduces the score.
            stats.RecordScoreDelta(-5);
            Assert.AreEqual(0, stats.Current.Score);

            // Undoing the penalty must NOT credit +5 — the penalty was clamped, so it applied nothing.
            stats.RecordMove(new ScoredMoveInfo(MoveType.Undo));
            Assert.AreEqual(0, stats.Current.Score);
        }

        [Test]
        public void RecordScoreDelta_PenaltyPartiallyClamped_ThenUndo_RestoresExactPreState()
        {
            using var stats = new SessionStatsService();
            stats.Initialize(new ZeroScoreRule());

            stats.RecordScoreDelta(3);   // score 3
            stats.RecordScoreDelta(-5);  // 3 - 5 = -2 → clamped to 0 (only 3 actually subtracted)
            Assert.AreEqual(0, stats.Current.Score);

            // Undo must restore the exact pre-penalty score (3), not over-credit to 5.
            stats.RecordMove(new ScoredMoveInfo(MoveType.Undo));
            Assert.AreEqual(3, stats.Current.Score);
        }

        // --- AddScoreToLastMove ---

        [Test]
        public void AddScoreToLastMove_FoldsIntoSingleUndoableDelta()
        {
            // Simulate a Spider run-completing move: RecordMove for the card move, then
            // AddScoreToLastMove for the auto-collected run bonus.
            // Exactly ONE MoveCount increment; a single Undo reverts both move delta and bonus.
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Tableau, PileType.Tableau)); // +0
            int scoreAfterMove = _service.Current.Score;
            Assert.AreEqual(1, _service.Current.MoveCount);

            _service.AddScoreToLastMove(10); // bonus for collected run, no extra MoveCount
            Assert.AreEqual(scoreAfterMove + 10, _service.Current.Score);
            Assert.AreEqual(1, _service.Current.MoveCount, "collection bonus must not increment MoveCount");

            _service.RecordMove(new ScoredMoveInfo(MoveType.Undo));
            Assert.AreEqual(scoreAfterMove, _service.Current.Score, "undo must revert both move delta and bonus");
            Assert.AreEqual(2, _service.Current.MoveCount); // undo itself is counted as a move
        }

        [Test]
        public void AddScoreToLastMove_WithPositiveMoveScore_UndoRefundsBoth()
        {
            // RecordMove T→Foundation (+10 via StubScoreRule), then bonus +20 from two collected runs.
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Tableau, PileType.Foundation)); // +10
            Assert.AreEqual(10, _service.Current.Score);
            Assert.AreEqual(1, _service.Current.MoveCount);

            _service.AddScoreToLastMove(20);
            Assert.AreEqual(30, _service.Current.Score);
            Assert.AreEqual(1, _service.Current.MoveCount);

            _service.RecordMove(new ScoredMoveInfo(MoveType.Undo));
            Assert.AreEqual(0, _service.Current.Score, "undo reverts 10 + 20 = 30");
            Assert.AreEqual(2, _service.Current.MoveCount);
        }

        [Test]
        public void AddScoreToLastMove_IgnoredWhenFrozen()
        {
            _service.RecordMove(new ScoredMoveInfo(MoveType.CardMove, PileType.Tableau, PileType.Foundation)); // +10
            _service.MarkWon();
            _service.AddScoreToLastMove(50);
            Assert.AreEqual(10, _service.Current.Score);
        }
    }
}
