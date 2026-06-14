using System.Collections.Generic;

namespace Model.Game
{
    /// <summary>
    /// Snapshot of the entire game board state.
    /// Published after SolitaireGameService.Initialize() and updated on every card move.
    /// </summary>
    public class TableState
    {
        /// <summary>Stock pile state (face-down card draw pile).</summary>
        public PileState Stock { get; }

        /// <summary>Waste pile state (cards flipped from Stock).</summary>
        public PileState Waste { get; }

        /// <summary>Foundation pile list (DealRule.FoundationCount piles).</summary>
        public IReadOnlyList<PileState> Foundations { get; }

        /// <summary>Tableau pile list (DealRule.TableauCount piles).</summary>
        public IReadOnlyList<PileState> Tableaus { get; }

        /// <summary>
        /// Number of cards drawn on the most recent stock click.
        /// Used to determine how many top waste cards to display fanned (spread).
        /// Stored in game state so Undo restores the fan correctly via history stack.
        /// Visual fanning only occurs when this value exceeds 1 (i.e., Draw-3 mode).
        /// Set to 0 on recycle, initial deal, or when waste is empty.
        /// </summary>
        public int WasteFanCount { get; }

        public TableState(
            PileState stock,
            PileState waste,
            List<PileState> foundations,
            List<PileState> tableaus,
            int wasteFanCount = 0)
        {
            Stock = stock;
            Waste = waste;
            Foundations = foundations;
            Tableaus = tableaus;
            WasteFanCount = wasteFanCount;
        }
    }
}
