using System;
using System.Collections.Generic;
using Model.Board;
using Model.Card;
using Service.GameService;

namespace Service.BoardGameService
{
    /// <summary>Pyramid: tap free cells (and the waste-top) to remove pairs summing to 13, or a King
    /// alone. Recyclable stock. Owns its tap-accumulator; all shared plumbing lives in the base.</summary>
    public sealed class PyramidGameService : BoardGameServiceBase
    {
        private readonly List<SelectedTarget> selection = new(); // accumulator lives in the Service

        private static readonly IReadOnlyList<BoardHint> DrawHints = new[] { BoardHint.Draw };
        private static readonly IReadOnlyList<BoardHint> RecycleHints = new[] { BoardHint.Recycle };

        public PyramidGameService(IShuffleStrategy shuffle) : base(shuffle) { }

        protected override void ResetSelectionState() => selection.Clear();

        public override void SelectCell(CellId id)
        {
            if (!BoardRules.IsFree(Layout, CurrentState, id)) return;
            HandleSelect(SelectedTarget.OfCell(id), CurrentState.CardAt(id));
            EmitSelection(BuildSelectionSnapshot());
        }

        public override void SelectWasteTop()
        {
            var top = CurrentState.WasteTop;
            if (top == null) return;
            HandleSelect(SelectedTarget.Waste(), top);
            EmitSelection(BuildSelectionSnapshot());
        }

        private PlayingCard CardOf(SelectedTarget t)
            => t.IsWaste ? CurrentState.WasteTop : CurrentState.CardAt(t.Cell);

        private void HandleSelect(SelectedTarget target, PlayingCard card)
        {
            if (card == null) return;

            int existing = selection.FindIndex(t => t.Equals(target));
            if (existing >= 0) { selection.RemoveAt(existing); return; } // toggle off

            selection.Add(target);
            if (TryResolve()) return;

            // Invalid: restart the selection with just the latest tap, then re-check (handles K after a non-match).
            selection.Clear();
            selection.Add(target);
            if (!TryResolve()) selection.Clear();
        }

        /// <summary>Evaluates the current selection; applies removal on Match. Returns true unless Invalid.</summary>
        private bool TryResolve()
        {
            var cards = new List<PlayingCard>(selection.Count);
            foreach (var t in selection) cards.Add(CardOf(t));

            switch (Rule.Evaluate(cards))
            {
                case MatchVerdict.Incomplete:
                    return true;
                case MatchVerdict.Match:
                    ApplyRemoval();
                    selection.Clear();
                    return true;
                default:
                    return false; // Invalid
            }
        }

        private void ApplyRemoval()
        {
            PushUndo();

            var cellIds = new List<CellId>(selection.Count);
            bool removeWaste = false;
            foreach (var t in selection)
            {
                if (t.IsWaste) removeWaste = true;
                else cellIds.Add(t.Cell);
            }

            var next = CurrentState.WithCellsRemoved(cellIds);
            if (removeWaste) next = next.WithWasteTopRemoved();
            PublishState(next);
        }

        private SelectionSnapshot BuildSelectionSnapshot()
        {
            var cells = new List<CellId>(selection.Count);
            bool waste = false;
            foreach (var t in selection)
            {
                if (t.IsWaste) waste = true;
                else cells.Add(t.Cell);
            }
            return new SelectionSnapshot(cells, waste);
        }

        public override bool HasAnyMove(BoardState state)
        {
            if (state.Stock.Count > 0) return true;
            if (CanRecycle(state)) return true;
            return EnumerateMatches(state).Count > 0;
        }

        public override IReadOnlyList<BoardHint> GetHints(BoardState state)
        {
            var matches = EnumerateMatches(state);
            if (matches.Count > 0) return matches;
            if (state.Stock.Count > 0) return DrawHints;
            if (CanRecycle(state)) return RecycleHints;
            return Array.Empty<BoardHint>();
        }

        /// <summary>Every removable match among the free cells + waste-top, as highlight targets.</summary>
        private List<BoardHint> EnumerateMatches(BoardState state)
        {
            var cells = new List<CellId>();
            var cards = new List<PlayingCard>();
            foreach (var id in BoardRules.FreeCells(Layout, state))
            {
                cells.Add(id);
                cards.Add(state.CardAt(id));
            }
            bool hasWaste = state.WasteTop != null;
            if (hasWaste) cards.Add(state.WasteTop); // waste is the last candidate, index == cells.Count

            var result = new List<BoardHint>();
            var single = new PlayingCard[1];
            var pair = new PlayingCard[2];
            for (int i = 0; i < cards.Count; i++)
            {
                single[0] = cards[i];
                if (Rule.Evaluate(single) == MatchVerdict.Match)
                {
                    result.Add(BoardHint.OfMatch(TargetsFor(cells, i, -1, hasWaste)));
                    continue;
                }
                for (int j = i + 1; j < cards.Count; j++)
                {
                    pair[0] = cards[i];
                    pair[1] = cards[j];
                    if (Rule.Evaluate(pair) == MatchVerdict.Match)
                        result.Add(BoardHint.OfMatch(TargetsFor(cells, i, j, hasWaste)));
                }
            }
            return result;
        }

        private static SelectionSnapshot TargetsFor(List<CellId> cells, int a, int b, bool hasWaste)
        {
            var picked = new List<CellId>(2);
            bool waste = false;
            AddTarget(cells, a, hasWaste, picked, ref waste);
            if (b >= 0) AddTarget(cells, b, hasWaste, picked, ref waste);
            return new SelectionSnapshot(picked, waste);
        }

        private static void AddTarget(List<CellId> cells, int index, bool hasWaste, List<CellId> picked, ref bool waste)
        {
            if (hasWaste && index == cells.Count) waste = true;
            else picked.Add(cells[index]);
        }

        private readonly struct SelectedTarget : IEquatable<SelectedTarget>
        {
            public bool IsWaste { get; }
            public CellId Cell { get; }
            private SelectedTarget(bool isWaste, CellId cell) { IsWaste = isWaste; Cell = cell; }
            public static SelectedTarget OfCell(CellId id) => new SelectedTarget(false, id);
            public static SelectedTarget Waste() => new SelectedTarget(true, default);
            public bool Equals(SelectedTarget other) => IsWaste == other.IsWaste && Cell.Equals(other.Cell);
            public override bool Equals(object obj) => obj is SelectedTarget t && Equals(t);
            public override int GetHashCode() => System.HashCode.Combine(IsWaste, Cell);
        }
    }
}
