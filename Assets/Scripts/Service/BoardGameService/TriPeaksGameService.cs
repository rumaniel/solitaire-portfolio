using System;
using System.Collections.Generic;
using Model.Board;
using Model.Card;
using Service.GameService;

namespace Service.BoardGameService
{
    /// <summary>TriPeaks: tap one free tableau card to play it onto the waste-top when their ranks are
    /// adjacent (A&#x2194;K wraps). No multi-tap accumulation, no recycle. The deal flips the first stock card
    /// to the waste so play has an anchor.</summary>
    public sealed class TriPeaksGameService : BoardGameServiceBase
    {
        private static readonly IReadOnlyList<BoardHint> DrawHints = new[] { BoardHint.Draw };
        private readonly PlayingCard[] pair = new PlayingCard[2];

        public TriPeaksGameService(IShuffleStrategy shuffle) : base(shuffle) { }

        protected override void ResetSelectionState() { } // no pending-selection accumulator

        protected override void OnDealt()
        {
            // Flip the first stock card to the waste so the player has a card to build on.
            if (CurrentState.Stock.Count > 0)
                CurrentState = CurrentState.WithStockDrawn();
        }

        public override void SelectCell(CellId id)
        {
            if (!BoardRules.IsFree(Layout, CurrentState, id)) return;
            var top = CurrentState.WasteTop;
            if (top == null) return;
            if (!IsPlayable(CurrentState.CardAt(id), top))
            {
                EmitInvalidTap(id); // free but rank-mismatched → invalid-move feedback
                return;
            }

            PushUndo();
            PublishState(CurrentState.WithCardPlayedToWaste(id));
            EmitSelection(SelectionSnapshot.Empty);
        }

        public override void SelectWasteTop() { } // the waste-top is the anchor, never a tap target

        public override bool HasAnyMove(BoardState state)
        {
            if (state.Stock.Count > 0) return true;
            var top = state.WasteTop;
            if (top == null) return false;
            foreach (var id in BoardRules.FreeCells(Layout, state))
                if (IsPlayable(state.CardAt(id), top)) return true;
            return false;
        }

        public override IReadOnlyList<BoardHint> GetHints(BoardState state)
        {
            var top = state.WasteTop;
            var result = new List<BoardHint>();
            if (top != null)
            {
                foreach (var id in BoardRules.FreeCells(Layout, state))
                    if (IsPlayable(state.CardAt(id), top))
                        result.Add(BoardHint.OfMatch(new SelectionSnapshot(new[] { id }, false)));
            }
            if (result.Count > 0) return result;
            if (state.Stock.Count > 0) return DrawHints;
            return Array.Empty<BoardHint>(); // no recycle: an empty stock with no play is stuck
        }

        private bool IsPlayable(PlayingCard card, PlayingCard wasteTop)
        {
            pair[0] = wasteTop;
            pair[1] = card;
            return (Rule.Evaluate(pair) == MatchVerdict.Match);
        }
    }
}
