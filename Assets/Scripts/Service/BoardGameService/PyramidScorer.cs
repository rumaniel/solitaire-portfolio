using System;
using Model.Board;

namespace Service.BoardGameService
{
    /// <summary>Pyramid scoring driven by the total-card delta: a positive delta is a removal (per-card
    /// points, plus the clear bonus on a win); otherwise a recycle or a stock draw. Stateless — reads
    /// everything from the supplied (prev, next), so the presenter owns the prior-state source of truth.</summary>
    public sealed class PyramidScorer : IBoardScorer
    {
        private readonly IBoardScoreRule rule;
        public PyramidScorer(IBoardScoreRule rule) { this.rule = rule ?? throw new ArgumentNullException(nameof(rule)); }

        public void Reset(BoardState initial) { } // stateless
        public void Undo() { }                    // stateless — nothing to revert

        public BoardScoreOutcome Evaluate(BoardState prev, BoardState next, bool won)
        {
            int removed = (TotalCards(prev) - TotalCards(next));
            if (removed > 0)
            {
                int pts = rule.ScoreForRemoval(removed);
                if (won) pts += rule.BoardClearedBonus;
                return new BoardScoreOutcome(pts, BoardScoreEvent.Cleared);
            }
            if (next.RecycleCount != prev.RecycleCount)
                return new BoardScoreOutcome(rule.ScoreForRecycle, BoardScoreEvent.Recycle);
            return new BoardScoreOutcome(rule.ScoreForStockDraw, BoardScoreEvent.Draw);
        }

        private static int TotalCards(BoardState s)
        {
            int occ = 0;
            foreach (var _ in s.OccupiedCells()) occ++;
            return (occ + s.Stock.Count + s.Waste.Count);
        }
    }
}
