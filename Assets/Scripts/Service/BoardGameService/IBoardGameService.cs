using System.Collections.Generic;
using Model.Board;
using R3;

namespace Service.BoardGameService
{
    /// <summary>
    /// Drives a layered-board solitaire (Pyramid now; Mahjong/TriPeaks later). Mirrors IGameService:
    /// Initialize, an Observable state stream, undo history, and restore for snapshots.
    /// </summary>
    public interface IBoardGameService
    {
        Observable<BoardState> OnBoardStateChanged { get; }
        BoardState CurrentState { get; }

        /// <summary>Pending tap-selection (cells + waste-top) for the View to highlight. Emits Empty after a match/clear.</summary>
        Observable<SelectionSnapshot> OnSelectionChanged { get; }
        SelectionSnapshot CurrentSelection { get; }

        /// <summary>Fires when a free card is tapped but cannot be played (e.g. TriPeaks rank mismatch),
        /// so the View can give invalid-move feedback. Default implementations never emit.</summary>
        Observable<CellId> OnInvalidTap { get; }

        BoardLayout Layout { get; }
        int? CurrentSeed { get; }

        /// <param name="maxRecycles">How many times the waste may be recycled back into the stock (0 = classic single pass).</param>
        void Initialize(BoardLayout layout, IBoardMatchRule rule, int? seed = null, int maxRecycles = 0);

        /// <summary>Tap a board cell. Ignored unless the cell is free.</summary>
        void SelectCell(CellId id);
        /// <summary>Tap the waste-top card (stock/waste games). Ignored when the waste is empty.</summary>
        void SelectWasteTop();
        /// <summary>Flip the top stock card to the waste. Ignored when the stock is empty.</summary>
        void DrawFromStock();
        /// <summary>Recycle the whole waste back into the stock (Pyramid). Ignored unless <see cref="CanRecycle"/>.</summary>
        void RecycleStock();
        /// <summary>True when the stock is empty but the waste can still be recycled (counts as an available move).</summary>
        bool CanRecycle(BoardState state);
        void ClearSelection();

        bool IsWon(BoardState state);
        bool HasAnyMove(BoardState state);
        /// <summary>Available next moves for the Hint button, best-first: every removable match, else a
        /// single Draw or Recycle suggestion, else empty (stuck).</summary>
        IReadOnlyList<BoardHint> GetHints(BoardState state);

        bool CanUndo { get; }
        void Undo();

        /// <summary>
        /// Past states enumerated <b>oldest-first</b> (chronological). This order is the contract for the
        /// <see cref="Restore"/> round-trip: pass the same sequence back unchanged. Typed as
        /// <see cref="IReadOnlyList{T}"/> so the indexed, ordered nature is part of the signature.
        /// Note: this deliberately differs from <c>IGameService.UndoHistory</c>, which is LIFO — a shared
        /// persistence layer must not assume a single convention across both services.
        /// </summary>
        IReadOnlyList<BoardState> UndoHistory { get; }

        /// <summary>Rebuilds state, seed, recycle limit, and undo history (oldest-first, per <see cref="UndoHistory"/>) for snapshot resume.</summary>
        void Restore(BoardLayout layout, IBoardMatchRule rule, int seed,
            BoardState state, IReadOnlyList<BoardState> undoHistory, int maxRecycles = 0);
    }
}
