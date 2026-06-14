using System;
using System.Collections.Generic;
using System.Linq;
using Model.Card;
using Model.Game;
using R3;
using Service.CardService;

namespace Service.GameService
{
    public class SolitaireGameService : IGameService, IDisposable
    {
        private readonly Subject<TableState> stateSubject = new();
        private readonly Stack<TableState> history = new();
        private readonly ShuffleStrategyProvider shuffleProvider;

        public SolitaireGameService(ShuffleStrategyProvider shuffleProvider)
        {
            this.shuffleProvider = shuffleProvider ?? throw new System.ArgumentNullException(nameof(shuffleProvider));
        }

        public IDealRule DealRule { get; private set; }
        public int? CurrentSeed { get; private set; }
        public TableState CurrentState { get; private set; }
        public Observable<TableState> OnTableStateChanged => stateSubject;
        public bool CanUndo => history.Count > 0;

        public bool CanDealStock =>
            DealRule != null && CurrentState != null
            && (DealRule.StockDealsToTableau
                ? (CurrentState.Stock.Cards.Count > 0
                    && (!DealRule.StockDealRequiresNoEmptyColumn
                        || CurrentState.Tableaus.All(t => t.Cards.Count > 0)))
                // Recycle needs a non-empty waste to refill from — an empty waste is a no-op.
                : (CurrentState.Stock.Cards.Count > 0
                    || (DealRule.CanRecycleStock && CurrentState.Waste.Cards.Count > 0)));

        // [Gateway] 향후 IGameGateway.InitializeAsync(IDealRule) 로 교체 예정
        public void Initialize(IDealRule dealRule, int? seed = null)
        {
            DealRule = dealRule;
            history.Clear();

            CurrentSeed = seed ?? DeckFactory.CreateRandomSeed();
            var strategy = shuffleProvider.Current;
            if (strategy is IReversePlayStrategy reverse)
            {
                CurrentState = reverse.BuildInitialState(CurrentSeed.Value, dealRule);
            }
            else
            {
                var deck = strategy.Shuffle(CurrentSeed.Value, dealRule);
                CurrentState = DealBuilder.Build(deck, dealRule);
            }
            stateSubject.OnNext(CurrentState);
        }

        // [Gateway] 향후 IGameGateway.ExecuteMoveAsync(MoveCardRequest) 로 교체 예정.
        // 순수 state mutation — 검증은 호출 전 CardService.TryMove()로 완료됐음을 전제.
        // 서버 전환 시: Presenter의 TryMove + ExecuteMove 두 호출이 gateway 단일 호출로 통합됨.
        public MoveCardResult ExecuteMove(MoveCardRequest request)
        {
            var sourcePile = GetPile(request.SourcePileId);
            var targetPile = GetPile(request.TargetPileId);

            if (sourcePile == null || targetPile == null)
                return MoveCardResult.Fail("Invalid source or target pile.");

            history.Push(CurrentState);

            var cardsToMove = new List<PlayingCard>();
            for (int i = request.SourceIndex; i < request.SourceIndex + request.Count; i++)
                cardsToMove.Add(sourcePile.Cards[i]);

            var updatedSource = RemoveCardsFrom(sourcePile, request.SourceIndex);
            var updatedTarget = AppendCards(targetPile, cardsToMove);

            int newFanCount = CurrentState.WasteFanCount;
            if (request.SourcePileId.Type == PileType.Waste)
                newFanCount = Math.Max(0, newFanCount - request.Count);

            var replaced = ReplacePile(CurrentState, updatedSource);
            CurrentState = ReplacePile(replaced, updatedTarget, newFanCount);
            CollectCompletedRuns();
            stateSubject.OnNext(CurrentState);
            return MoveCardResult.Success();
        }

        public bool IsWon(TableState state) => state.Foundations.All(f => f.Cards.Count == DealRule.PerSuitCardCount);

        // [Gateway] 향후 IGameGateway.DrawFromStockAsync() 로 교체 예정
        public void DrawFromStock()
        {
            var stock = CurrentState.Stock;
            var waste = CurrentState.Waste;

            if (DealRule.StockDealsToTableau)
            {
                DealStockToTableaus();
                return;
            }

            // Early exit before pushing history — no-op should not create an undo entry
            if (stock.Cards.Count == 0 && !DealRule.CanRecycleStock) return;

            history.Push(CurrentState);

            PileState newStock, newWaste;
            int newFanCount;
            if (stock.Cards.Count == 0)
            {

                // Recycle: reverse Waste back into Stock face-down
                var recycled = new List<PlayingCard>(waste.Cards);
                recycled.Reverse();
                newStock = new PileState(stock.Id, recycled, recycled.Count); // all face-down
                newWaste = new PileState(waste.Id, new List<PlayingCard>(), 0);
                newFanCount = 0;
            }
            else
            {
                // Draw StockDrawCount cards from top of Stock to Waste face-up
                int drawCount = Math.Min(DealRule.StockDrawCount, stock.Cards.Count);
                var newStockCards = new List<PlayingCard>(stock.Cards);
                var newWasteCards = new List<PlayingCard>(waste.Cards);
                for (int i = 0; i < drawCount; i++)
                {
                    var top = newStockCards[newStockCards.Count - 1];
                    newStockCards.RemoveAt(newStockCards.Count - 1);
                    newWasteCards.Add(top);
                }
                newStock = new PileState(stock.Id, newStockCards, newStockCards.Count); // all face-down
                newWaste = new PileState(waste.Id, newWasteCards, 0); // all face-up
                newFanCount = drawCount;
            }

            var replaced = ReplacePile(CurrentState, newStock);
            CurrentState = ReplacePile(replaced, newWaste, newFanCount);
            stateSubject.OnNext(CurrentState);
        }

        /// <summary>
        /// EastHaven-style stock draw: deal one card from Stock to each Tableau column
        /// (face-up). No Waste, no recycle. No-op when Stock is empty.
        /// </summary>
        private void DealStockToTableaus()
        {
            var stock = CurrentState.Stock;
            if (stock.Cards.Count == 0) return;
            if (DealRule.StockDealRequiresNoEmptyColumn
                && CurrentState.Tableaus.Any(t => t.Cards.Count == 0)) return;

            history.Push(CurrentState);

            var newStockCards = new List<PlayingCard>(stock.Cards);
            var newTableaus = new List<PileState>(CurrentState.Tableaus);
            int dealCount = Math.Min(newTableaus.Count, newStockCards.Count);

            for (int i = 0; i < dealCount; i++)
            {
                var top = newStockCards[newStockCards.Count - 1];
                newStockCards.RemoveAt(newStockCards.Count - 1);

                var t = newTableaus[i];
                var cards = new List<PlayingCard>(t.Cards) { top };
                newTableaus[i] = new PileState(t.Id, cards, t.FaceUpFromIndex);
            }

            var newStock = new PileState(stock.Id, newStockCards, newStockCards.Count);
            CurrentState = new TableState(
                newStock,
                CurrentState.Waste,
                CurrentState.Foundations.ToList(),
                newTableaus,
                CurrentState.WasteFanCount);
            CollectCompletedRuns();
            stateSubject.OnNext(CurrentState);
        }

        public void Undo()
        {
            if (!CanUndo) return;
            CurrentState = history.Pop();
            stateSubject.OnNext(CurrentState);
        }

        /// <summary>
        /// Undo history stack exposed as read-only.
        /// Enumeration order is LIFO (most recent state first), matching Stack&lt;T&gt; semantics.
        /// </summary>
        public IReadOnlyCollection<TableState> UndoHistory => history;

        /// <summary>
        /// Restores game state from a snapshot. <paramref name="undoHistory"/> must be in
        /// LIFO order (most recent first) — the same order as <see cref="UndoHistory"/> enumeration.
        /// </summary>
        public void Restore(IDealRule dealRule, int seed, TableState state, IReadOnlyList<TableState> undoHistory)
        {
            DealRule = dealRule;
            CurrentSeed = seed;
            history.Clear();
            // undoHistory is LIFO (most recent first). Push in reverse so that
            // the most recent state ends up on top of the stack.
            for (int i = undoHistory.Count - 1; i >= 0; i--)
                history.Push(undoHistory[i]);
            CurrentState = state;
            stateSubject.OnNext(CurrentState);
        }

        public void Dispose() => stateSubject.Dispose();

        /// <summary>
        /// Removes every face-up K..A same-suit run sitting on a tableau top and stacks it on
        /// the first empty foundation. Runs inside the triggering move's state transition —
        /// callers mutate CurrentState, then collect, then emit — so move + collection share
        /// one history entry and one emission (a single atomic undo step).
        /// </summary>
        private void CollectCompletedRuns()
        {
            if (!DealRule.AutoCollectCompletedRuns) return;

            const int runLength = 13;
            var state = CurrentState;
            for (int t = 0; t < state.Tableaus.Count; t++)
            {
                var pile = state.Tableaus[t];
                if (pile.Cards.Count < runLength) continue;

                int start = pile.Cards.Count - runLength;
                if (pile.FaceUpFromIndex > start) continue;

                bool isRun = pile.Cards[start].Rank == Rank.King;
                for (int i = start; isRun && i < pile.Cards.Count - 1; i++)
                {
                    var bottom = pile.Cards[i];
                    var top = pile.Cards[i + 1];
                    isRun = top.Suit == bottom.Suit && (int)top.Rank == (int)bottom.Rank - 1;
                }
                if (!isRun) continue;

                var emptyFoundation = state.Foundations.FirstOrDefault(f => f.Cards.Count == 0);
                if (emptyFoundation == null) continue;

                var collected = pile.Cards.Skip(start).ToList();
                state = ReplacePile(state, RemoveCardsFrom(pile, start));
                state = ReplacePile(state, new PileState(emptyFoundation.Id, collected, 0));
                CurrentState = state;
                // After collecting, re-check the same column index — the reveal may expose
                // another completed run on this same column.
                t--;
            }
        }

        private PileState GetPile(PileId id) =>
            CurrentState.Stock.Id.Equals(id) ? CurrentState.Stock :
            CurrentState.Waste.Id.Equals(id) ? CurrentState.Waste :
            CurrentState.Foundations.FirstOrDefault(p => p.Id.Equals(id)) ??
            CurrentState.Tableaus.FirstOrDefault(p => p.Id.Equals(id));

        private PileState RemoveCardsFrom(PileState pile, int fromIndex)
        {
            var cards = pile.Cards.Take(fromIndex).ToList();
            // Flip new top card if it was face-down (Klondike flip-on-reveal)
            var faceUpFrom = pile.FaceUpFromIndex;
            if (cards.Count > 0 && faceUpFrom >= cards.Count)
                faceUpFrom = cards.Count - 1;
            else if (cards.Count == 0)
                faceUpFrom = 0; // empty pile → reset face-up index to 0

            return new PileState(pile.Id, cards, faceUpFrom);
        }

        private PileState AppendCards(PileState pile, List<PlayingCard> newCards)
        {
            var cards = new List<PlayingCard>(pile.Cards);
            cards.AddRange(newCards);
            return new PileState(pile.Id, cards, pile.FaceUpFromIndex);
        }

        private TableState ReplacePile(TableState state, PileState updated, int? wasteFanCountOverride = null)
        {
            var stock = state.Stock.Id.Equals(updated.Id) ? updated : state.Stock;
            var waste = state.Waste.Id.Equals(updated.Id) ? updated : state.Waste;
            var foundations = state.Foundations.Select(p => p.Id.Equals(updated.Id) ? updated : p).ToList();
            var tableaus = state.Tableaus.Select(p => p.Id.Equals(updated.Id) ? updated : p).ToList();
            return new TableState(stock, waste, foundations, tableaus,
                wasteFanCountOverride ?? state.WasteFanCount);
        }
    }
}
