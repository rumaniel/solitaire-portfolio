using Model.Stats;
using R3;

namespace Service.StatsService
{
    public interface ISessionStatsService
    {
        SessionStats Current { get; }
        Observable<SessionStats> OnStatsChanged { get; }
        void Initialize(IScoreRule scoreRule);
        void RecordMove(ScoredMoveInfo moveInfo);
        /// <summary>Adds an explicit score delta and counts it as one move. For board games that compute their own
        /// per-match score (bypasses the pile-type inference in <see cref="RecordMove"/>). A later
        /// <see cref="RecordMove"/> with <see cref="MoveType.Undo"/> subtracts the most recent delta.</summary>
        void RecordScoreDelta(int delta);
        /// <summary>Folds a bonus score into the most recently recorded move's undoable delta WITHOUT
        /// incrementing MoveCount. Used to credit auto-collected run bonuses as part of the triggering
        /// move so a single Undo reverts both the move score and the collection bonus atomically.</summary>
        void AddScoreToLastMove(int delta);
        void RecordHintUsed();
        void Tick(float deltaTime);
        void Pause();
        void Resume();
        bool IsPaused { get; }
        void MarkWon();
        void MarkLost();
        void Restore(IScoreRule scoreRule, SessionStats stats);
    }
}
