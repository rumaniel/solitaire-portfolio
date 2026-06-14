using Model.Board;

namespace Service.BoardGameService
{
    /// <summary>What kind of move a board state-transition was — drives the move sound.</summary>
    public enum BoardScoreEvent { None, Cleared, Draw, Recycle }

    /// <summary>Points to apply plus the move kind for a single state transition.</summary>
    public readonly struct BoardScoreOutcome
    {
        public int Points { get; }
        public BoardScoreEvent Event { get; }
        public BoardScoreOutcome(int points, BoardScoreEvent ev) { Points = points; Event = ev; }
    }

    /// <summary>Stateful, game-specific scorer. Turns a (prev → next) board transition into points and a
    /// move kind. The presenter calls <see cref="Reset"/> at deal/restore, <see cref="Evaluate"/> on each
    /// forward move, and <see cref="Undo"/> when a move is reverted (so any internal accumulators stay in
    /// sync — the presenter skips <see cref="Evaluate"/> during undo). Accumulators (e.g. TriPeaks streak)
    /// live here, not in the immutable score rule.</summary>
    public interface IBoardScorer
    {
        void Reset(BoardState initial);
        BoardScoreOutcome Evaluate(BoardState prev, BoardState next, bool won);

        /// <summary>Reverts the internal state advanced by the most recent <see cref="Evaluate"/> (one
        /// forward move). No-op for a stateless scorer.</summary>
        void Undo();
    }
}
