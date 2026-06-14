using System;
using Model.Game;
using Model.Stats;
using R3;

namespace Service.StatsService
{
    public class SessionStatsService : ISessionStatsService, IDisposable
    {
        private readonly Subject<SessionStats> statsSubject = new();
        private IScoreRule scoreRule;
        private const float MaxTickDelta = 1f;
        private bool frozen;
        private int lastScoreDelta;
        private int lastEmittedSecond = -1;

        public SessionStats Current { get; private set; } = new();
        public Observable<SessionStats> OnStatsChanged => statsSubject;
        public bool IsPaused { get; private set; }

        public void Initialize(IScoreRule scoreRule)
        {
            this.scoreRule = scoreRule;
            Current = new SessionStats();
            frozen = false;
            IsPaused = false;
            lastScoreDelta = 0;
            lastEmittedSecond = -1;
            statsSubject.OnNext(Current.Snapshot());
        }

        public void RecordMove(ScoredMoveInfo moveInfo)
        {
            if (frozen) return;

            Current.MoveCount++;

            if (moveInfo.MoveType == MoveType.Undo)
            {
                Current.UndoUsed = true;
                Current.Score = Math.Max(0, Current.Score - lastScoreDelta);
                lastScoreDelta = 0;
            }
            else
            {
                int newScore = Math.Max(0, Current.Score + CalculateScoreDelta(moveInfo));
                lastScoreDelta = newScore - Current.Score; // store the APPLIED delta (post-clamp) so an undo reverses exactly what was applied
                Current.Score = newScore;
            }

            statsSubject.OnNext(Current.Snapshot());
        }

        public void RecordScoreDelta(int delta)
        {
            if (frozen) return;

            Current.MoveCount++;
            int newScore = Math.Max(0, Current.Score + delta);
            lastScoreDelta = newScore - Current.Score; // APPLIED delta (post-clamp): a penalty floored at 0 refunds 0 on undo, not the raw delta
            Current.Score = newScore;
            statsSubject.OnNext(Current.Snapshot());
        }

        public void AddScoreToLastMove(int delta)
        {
            if (frozen) return;

            // Accumulate into the existing reversible unit: clamp the score but grow lastScoreDelta
            // by exactly what was applied, so a single Undo later reverts both the base move and this bonus.
            int newScore = Math.Max(0, Current.Score + delta);
            lastScoreDelta += newScore - Current.Score;
            Current.Score = newScore;
            statsSubject.OnNext(Current.Snapshot());
        }

        public void Tick(float deltaTime)
        {
            if (frozen || IsPaused) return;
            Current.ElapsedSeconds += Math.Min(deltaTime, MaxTickDelta);

            // Throttled emit: publish once per integer second change (1 Hz)
            int sec = (int)Current.ElapsedSeconds;
            if (sec != lastEmittedSecond)
            {
                lastEmittedSecond = sec;
                statsSubject.OnNext(Current.Snapshot());
            }
        }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        public void RecordHintUsed()
        {
            if (frozen) return;
            Current.HintUsed = true;
            Current.HintCount++;
            statsSubject.OnNext(Current.Snapshot());
        }

        public void MarkWon()
        {
            if (frozen) return;
            frozen = true;
            Current.IsWon = true;
            Current.IsFinished = true;
            statsSubject.OnNext(Current.Snapshot());
        }

        public void MarkLost()
        {
            if (frozen) return;
            frozen = true;
            Current.IsWon = false;
            Current.IsFinished = true;
            statsSubject.OnNext(Current.Snapshot());
        }

        public void Restore(IScoreRule scoreRule, SessionStats stats)
        {
            this.scoreRule = scoreRule;
            Current = stats.Snapshot();
            frozen = false;
            IsPaused = false;
            lastScoreDelta = 0;
            lastEmittedSecond = -1;
            statsSubject.OnNext(Current.Snapshot());
        }

        public void Dispose() => statsSubject.Dispose();

        private int CalculateScoreDelta(ScoredMoveInfo info)
        {
            int delta = 0;

            switch (info.MoveType)
            {
                case MoveType.CardMove:
                    delta += CalculateCardMoveScore(info.SourcePileType, info.TargetPileType);
                    if (info.CausedTableauReveal)
                        delta += scoreRule.TableauReveal;
                    break;
                case MoveType.StockRecycle:
                    delta += scoreRule.StockRecycle;
                    break;
                case MoveType.StockDraw:
                    break;
            }

            return delta;
        }

        private int CalculateCardMoveScore(PileType source, PileType target)
        {
            if (source == PileType.Waste && target == PileType.Tableau)
                return scoreRule.WasteToTableau;
            if (source == PileType.Waste && target == PileType.Foundation)
                return scoreRule.WasteToFoundation;
            if (source == PileType.Tableau && target == PileType.Foundation)
                return scoreRule.TableauToFoundation;
            if (source == PileType.Foundation && target == PileType.Tableau)
                return scoreRule.FoundationToTableau;
            return 0;
        }
    }
}
