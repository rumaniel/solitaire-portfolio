using System;
using System.Collections.Generic;
using Model.Board;

namespace Service.BoardGameService
{
    /// <summary>TriPeaks scoring. A play (one cell cleared, waste +1) scores 50×streak with a peak bonus
    /// when an apex clears; a stock draw (stock −1, waste +1, occupancy unchanged) resets the streak and
    /// scores the draw penalty. Holds the streak + peaks-cleared counters; <see cref="Reset"/> derives
    /// peaks-cleared from any apexes already empty in the start state (correct on a resumed game).</summary>
    public sealed class TriPeaksScorer : IBoardScorer
    {
        private readonly ITriPeaksScoreRule rule;
        private readonly HashSet<CellId> apex;
        // Per-forward-move snapshots of (streak, peaksCleared) so an undo can revert the counters — the
        // presenter skips Evaluate during undo, so without this the streak/peak ordinal would desync.
        private readonly Stack<(int streak, int peaksCleared)> history = new();
        private int streak;
        private int peaksCleared;

        public TriPeaksScorer(ITriPeaksScoreRule rule, IEnumerable<CellId> apexCells)
        {
            this.rule = rule ?? throw new ArgumentNullException(nameof(rule));
            if (apexCells == null) throw new ArgumentNullException(nameof(apexCells));
            apex = new HashSet<CellId>(apexCells);
        }

        public void Reset(BoardState initial)
        {
            streak = 0;
            peaksCleared = 0;
            history.Clear();
            foreach (var id in apex)
                if ((id.Value < initial.CellCount) && !initial.HasCard(id)) peaksCleared++;
        }

        public BoardScoreOutcome Evaluate(BoardState prev, BoardState next, bool won)
        {
            int prevOcc = OccupiedCount(prev);
            int nextOcc = OccupiedCount(next);

            // A play: exactly one cell cleared and the waste grew by one.
            if ((nextOcc == (prevOcc - 1)) && (next.Waste.Count == (prev.Waste.Count + 1)))
            {
                history.Push((streak, peaksCleared));
                streak++;
                int pts = rule.PointsForStreak(streak);
                var removed = FindRemovedCell(prev, next);
                if (removed.HasValue && apex.Contains(removed.Value))
                {
                    peaksCleared++;
                    pts += rule.PeakBonus(peaksCleared);
                }
                return new BoardScoreOutcome(pts, BoardScoreEvent.Cleared);
            }

            // A deal: stock shrank by one, waste grew by one, occupancy unchanged.
            if ((nextOcc == prevOcc) && (next.Stock.Count == (prev.Stock.Count - 1))
                && (next.Waste.Count == (prev.Waste.Count + 1)))
            {
                history.Push((streak, peaksCleared));
                streak = 0;
                return new BoardScoreOutcome(rule.StockDrawPenalty, BoardScoreEvent.Draw);
            }

            return new BoardScoreOutcome(0, BoardScoreEvent.None);
        }

        public void Undo()
        {
            if (history.Count == 0) return; // nothing scored yet (e.g. undo right after a fresh deal)
            (streak, peaksCleared) = history.Pop();
        }

        private static int OccupiedCount(BoardState s)
        {
            int n = 0;
            foreach (var _ in s.OccupiedCells()) n++;
            return n;
        }

        private static CellId? FindRemovedCell(BoardState prev, BoardState next)
        {
            for (int i = 0; i < prev.CellCount; i++)
            {
                var id = new CellId(i);
                if (prev.HasCard(id) && !next.HasCard(id)) return id;
            }
            return null;
        }
    }
}
