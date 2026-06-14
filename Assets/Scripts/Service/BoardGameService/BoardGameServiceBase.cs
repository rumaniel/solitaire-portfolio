using System;
using System.Collections.Generic;
using Model.Board;
using Model.Card;
using R3;
using Service.GameService;

namespace Service.BoardGameService
{
    /// <summary>
    /// Shared scaffolding for the cover-based board games (Pyramid, TriPeaks): the state/selection
    /// streams, deal+split, stock draw/recycle, undo stack, snapshot restore, win check, and selection
    /// emission. Subclasses own the game-specific tap/apply/move-detection/hint logic.
    /// </summary>
    public abstract class BoardGameServiceBase : IBoardGameService, IDisposable
    {
        private readonly IShuffleStrategy shuffle;
        private readonly Subject<BoardState> stateSubject = new();
        private readonly Subject<SelectionSnapshot> selectionSubject = new();
        private readonly Subject<CellId> invalidTapSubject = new();
        private readonly List<BoardState> undoStack = new();

        protected IBoardMatchRule Rule { get; private set; }
        protected int MaxRecycles { get; private set; }

        public BoardLayout Layout { get; private set; }
        public BoardState CurrentState { get; protected set; }
        public int? CurrentSeed { get; private set; }
        public Observable<BoardState> OnBoardStateChanged => stateSubject;
        public SelectionSnapshot CurrentSelection { get; private set; } = SelectionSnapshot.Empty;
        public Observable<SelectionSnapshot> OnSelectionChanged => selectionSubject;
        public Observable<CellId> OnInvalidTap => invalidTapSubject;

        protected BoardGameServiceBase(IShuffleStrategy shuffle)
        {
            this.shuffle = shuffle ?? throw new ArgumentNullException(nameof(shuffle));
        }

        public void Initialize(BoardLayout layout, IBoardMatchRule rule, int? seed = null, int maxRecycles = 0)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            MaxRecycles = maxRecycles;

            int actualSeed = seed ?? DeckFactory.CreateRandomSeed();
            CurrentSeed = actualSeed;

            var deck = shuffle.Shuffle(actualSeed);
            if (deck.Count < layout.Count)
                throw new InvalidOperationException(
                    $"Deck ({deck.Count}) smaller than layout cell count ({layout.Count}).");

            var cellCards = new List<PlayingCard>(layout.Count);
            for (int i = 0; i < layout.Count; i++) cellCards.Add(deck[i]);

            var stock = new List<PlayingCard>(deck.Count - layout.Count);
            for (int i = layout.Count; i < deck.Count; i++) stock.Add(deck[i]);

            CurrentState = new BoardState(cellCards, stock, waste: null);
            undoStack.Clear();
            ResetSelectionState();
            OnDealt(); // hook: TriPeaks flips the first stock card to the waste so play has an anchor.
            stateSubject.OnNext(CurrentState);
            EmitSelection(SelectionSnapshot.Empty);
        }

        public void DrawFromStock()
        {
            if (CurrentState.Stock.Count == 0) return;
            PushUndo();
            ResetSelectionState();
            CurrentState = CurrentState.WithStockDrawn();
            stateSubject.OnNext(CurrentState);
            EmitSelection(SelectionSnapshot.Empty);
        }

        public bool CanRecycle(BoardState state)
            => state.Stock.Count == 0 && state.Waste.Count > 0 && state.RecycleCount < MaxRecycles;

        public void RecycleStock()
        {
            if (!CanRecycle(CurrentState)) return;
            PushUndo();
            ResetSelectionState();
            CurrentState = CurrentState.WithStockRecycled();
            stateSubject.OnNext(CurrentState);
            EmitSelection(SelectionSnapshot.Empty);
        }

        public void ClearSelection()
        {
            ResetSelectionState();
            EmitSelection(SelectionSnapshot.Empty);
        }

        public bool IsWon(BoardState state) => !state.AnyOccupied();

        public bool CanUndo => undoStack.Count > 0;

        public void Undo()
        {
            if (undoStack.Count == 0) return;
            int last = undoStack.Count - 1;
            CurrentState = undoStack[last];
            undoStack.RemoveAt(last);
            ResetSelectionState();
            stateSubject.OnNext(CurrentState);
            EmitSelection(SelectionSnapshot.Empty);
        }

        public IReadOnlyList<BoardState> UndoHistory => undoStack;

        public void Restore(BoardLayout layout, IBoardMatchRule rule, int seed,
            BoardState state, IReadOnlyList<BoardState> undoHistory, int maxRecycles = 0)
        {
            // Validate EVERYTHING before mutating any field — Restore must be atomic.
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.CellCount != layout.Count)
                throw new ArgumentException(
                    $"Snapshot state cell count ({state.CellCount}) does not match layout ({layout.Count}).",
                    nameof(state));
            if (undoHistory != null)
            {
                foreach (var past in undoHistory)
                {
                    if (past == null)
                        throw new ArgumentException("Undo history contains a null state.", nameof(undoHistory));
                    if (past.CellCount != layout.Count)
                        throw new ArgumentException(
                            $"Undo history state cell count ({past.CellCount}) does not match layout ({layout.Count}).",
                            nameof(undoHistory));
                }
            }

            Layout = layout;
            Rule = rule;
            MaxRecycles = maxRecycles;
            CurrentSeed = seed;
            undoStack.Clear();
            if (undoHistory != null) undoStack.AddRange(undoHistory);
            ResetSelectionState();

            CurrentState = state;
            stateSubject.OnNext(CurrentState);
            EmitSelection(SelectionSnapshot.Empty);
        }

        public virtual void Dispose()
        {
            stateSubject.Dispose();
            selectionSubject.Dispose();
            invalidTapSubject.Dispose();
        }

        // --- protected helpers / hooks for subclasses ---

        protected void PushUndo() => undoStack.Add(CurrentState);

        protected void PublishState(BoardState state)
        {
            CurrentState = state;
            stateSubject.OnNext(CurrentState);
        }

        /// <summary>Emits a selection snapshot, skipping a redundant emission equal to the current one.</summary>
        protected void EmitSelection(SelectionSnapshot next)
        {
            if (next.Equals(CurrentSelection)) return;
            CurrentSelection = next;
            selectionSubject.OnNext(CurrentSelection);
        }

        /// <summary>Signals that a free cell was tapped but the move was rejected (View shows shake/sound).</summary>
        protected void EmitInvalidTap(CellId id) => invalidTapSubject.OnNext(id);

        /// <summary>Clears the subclass's pending-selection representation (Pyramid: its accumulator; TriPeaks: nothing).</summary>
        protected abstract void ResetSelectionState();

        /// <summary>Post-deal hook. Default no-op (Pyramid); TriPeaks flips the first stock card to the waste.</summary>
        protected virtual void OnDealt() { }

        // --- game-specific surface ---

        public abstract void SelectCell(CellId id);
        public abstract void SelectWasteTop();
        public abstract bool HasAnyMove(BoardState state);
        public abstract IReadOnlyList<BoardHint> GetHints(BoardState state);
    }
}
