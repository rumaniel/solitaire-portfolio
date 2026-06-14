using System.Collections.Generic;
using Component.Card;
using Data.Card;
using Model.Board;
using Model.Card;
using R3;
using UnityEngine;

namespace Component.Board
{
    /// <summary>
    /// Tap-based, fixed-anchor renderer for cover-based board games (Pyramid now). One <see cref="UICard"/>
    /// per board <see cref="CellId"/> at <c>cellAnchors[CellId.Value]</c>; plus a stock (face-down) and a
    /// waste (face-up top). Emits taps as cell / stock / waste events; the presenter owns all game logic.
    /// </summary>
    public class UIBoardController : MonoBehaviour
    {
        [Header("Anchors (element index = CellId.Value; order apex→base for correct overlap)")]
        [SerializeField] private RectTransform[] cellAnchors;
        [SerializeField] private RectTransform stockAnchor;
        [SerializeField] private RectTransform wasteAnchor;

        [Header("Prefab")]
        [SerializeField] private UICard cardPrefab;
        [SerializeField] private BoardRemovalAnimator removalAnimator;

        private CardSpriteSet currentSpriteSet;

        private readonly Dictionary<int, UICard> cardByCell = new();   // CellId.Value -> view
        private readonly Dictionary<UICard, CellId> cellByCard = new();
        private UICard stockCard;
        private UICard wasteCard;
        private int lastWasteCount;
        private int lastRecycleCount;

        private readonly Subject<CellId> cellTapped = new();
        private readonly Subject<Unit> stockTapped = new();
        private readonly Subject<Unit> wasteTapped = new();

        public Observable<CellId> OnCellTapped => cellTapped;
        public Observable<Unit> OnStockTapped => stockTapped;
        public Observable<Unit> OnWasteTapped => wasteTapped;

        public void ApplySpriteSet(CardSpriteSet spriteSet)
        {
            currentSpriteSet = spriteSet;
            if (spriteSet == null) return;
            foreach (var c in cardByCell.Values)
                if (c != null) c.SetSpriteSet(spriteSet);
            if (stockCard != null) stockCard.SetSpriteSet(spriteSet);
            if (wasteCard != null) wasteCard.SetSpriteSet(spriteSet);
        }

        /// <summary>Reconciles spawned views with the state: spawn newly-present cells, despawn removed ones, refresh stock/waste.</summary>
        /// <param name="animateRemovals">True for forward play (matched cards spin out); false for undo/revert (instant).</param>
        /// <param name="canRecycle">True when an empty stock can still be recycled — keeps the stock slot tappable.</param>
        public void RenderBoard(BoardState state, bool animateRemovals = true, bool canRecycle = false)
        {
            var newWasteTop = state.WasteTop;
            bool wasteGrew = state.Waste.Count > lastWasteCount;
            bool adoptedPlay = false;

            for (int value = 0; value < state.CellCount; value++)
            {
                var id = new CellId(value);
                bool has = state.HasCard(id);
                bool spawned = cardByCell.ContainsKey(value);
                if (has && !spawned) SpawnCell(id, state.CardAt(id));
                else if (!has && spawned)
                {
                    // TriPeaks play: this cell's card is the new waste-top → fly it to the waste and adopt it.
                    if (animateRemovals && wasteGrew && !adoptedPlay && IsPlayToWaste(value, newWasteTop))
                    {
                        adoptedPlay = true;
                        PlayCellToWaste(value);
                    }
                    else DespawnCell(value, animateRemovals);
                }
            }
            RenderStock(state, canRecycle);
            RenderWaste(state, animateRemovals, suppressSpawn: adoptedPlay);
        }

        private bool IsPlayToWaste(int value, PlayingCard newWasteTop)
        {
            if (newWasteTop == null) return false;
            return cardByCell.TryGetValue(value, out var card) && card != null
                   && card.GetCard() != null && card.GetCard().Equals(newWasteTop);
        }

        /// <summary>Flies the played cell card to the waste anchor; the previous waste card stays visible
        /// underneath and is only swapped to the played card once the flier lands (see OnFlyToWasteArrived).</summary>
        private void PlayCellToWaste(int value)
        {
            if (!cardByCell.TryGetValue(value, out var card) || card == null) return;
            cardByCell.Remove(value);
            cellByCard.Remove(card);
            card.OnPointerClickEvent.RemoveListener(OnCellCardClicked);

            // Do NOT remove the previous waste card here — it must stay shown during the flight. RenderWaste
            // is suppressed this frame (adoptedPlay), so its old face persists until the flier arrives.
            float wasteScale = wasteAnchor != null ? wasteAnchor.localScale.x : 1f;
            removalAnimator.FlyToWaste(card, wasteAnchor, wasteScale, () => OnFlyToWasteArrived(card));
        }

        /// <summary>Called when a played card finishes flying to the waste. The previous waste card stayed
        /// visible during the flight; it now takes on the flown card's face and the flier is dropped. If the
        /// waste happened to be empty, the flier is adopted as the waste card instead.</summary>
        private void OnFlyToWasteArrived(UICard flier)
        {
            if (flier == null) return;
            if (wasteCard != null)
            {
                wasteCard.SetCard(flier.GetCard()); // previous waste card becomes the played card on arrival
                Destroy(flier.gameObject);
                return;
            }
            if (wasteAnchor != null)
            {
                flier.rectTransform.SetParent(wasteAnchor, false);
                flier.rectTransform.anchoredPosition = Vector2.zero;
                flier.rectTransform.localScale = Vector3.one; // anchor itself carries any scale
            }
            flier.Enable();
            flier.OnPointerClickEvent.AddListener(OnWasteClicked);
            wasteCard = flier;
        }

        /// <summary>Sets which cells are interactable (free). Locked cells cannot be tapped and show a dark cover.</summary>
        public void SetFreeCells(ICollection<CellId> freeCells)
        {
            foreach (var kv in cardByCell)
            {
                if (kv.Value == null) continue;
                bool free = freeCells.Contains(new CellId(kv.Key));
                if (free) { kv.Value.Enable(); kv.Value.SetCovered(false); }
                else { kv.Value.Disable(); kv.Value.SetCovered(true); }
            }
        }

        /// <summary>Highlights the pending tap-selection. Sole driver of the per-card highlight visual.</summary>
        public void SetSelection(SelectionSnapshot selection)
        {
            foreach (var kv in cardByCell)
            {
                if (kv.Value == null) continue;
                kv.Value.SetHighlight(selection != null && selection.Contains(new CellId(kv.Key)));
            }
            if (wasteCard != null)
                wasteCard.SetHighlight(selection != null && selection.WasteSelected);
        }

        /// <summary>Glow the stock pile as a Draw/Recycle hint affordance (reuses the selection glow).</summary>
        public void SetStockHighlight(bool on)
        {
            if (stockCard != null) stockCard.SetHighlight(on);
        }

        /// <summary>Shakes the card at a cell as invalid-move feedback (no state change).</summary>
        public void ShakeCell(CellId id)
        {
            if (cardByCell.TryGetValue(id.Value, out var card) && card != null) card.Shake();
        }

        public void DespawnAll()
        {
            foreach (var kv in cardByCell)
            {
                if (kv.Value == null) continue;
                kv.Value.OnPointerClickEvent.RemoveListener(OnCellCardClicked);
                Destroy(kv.Value.gameObject);
            }
            cardByCell.Clear();
            cellByCard.Clear();
            if (stockCard != null) { stockCard.OnPointerClickEvent.RemoveListener(OnStockClicked); Destroy(stockCard.gameObject); stockCard = null; }
            if (wasteCard != null) { wasteCard.OnPointerClickEvent.RemoveListener(OnWasteClicked); Destroy(wasteCard.gameObject); wasteCard = null; }
            lastWasteCount = 0;
            lastRecycleCount = 0;
        }

        private void SpawnCell(CellId id, PlayingCard card)
        {
            var anchor = AnchorFor(id);
            var view = Instantiate(cardPrefab, anchor != null ? anchor : transform);
            view.rectTransform.anchoredPosition = Vector2.zero;
            view.IsDraggable = false;              // board is tap-only
            if (currentSpriteSet != null) view.SetSpriteSet(currentSpriteSet);
            view.SetCard(card);
            view.OpenImmediate();                 // board cards are dealt face-up
            view.OnPointerClickEvent.AddListener(OnCellCardClicked);
            cardByCell[id.Value] = view;
            cellByCard[view] = id;
        }

        private void DespawnCell(int value, bool animate)
        {
            if (!cardByCell.TryGetValue(value, out var view)) return;
            cardByCell.Remove(value);
            if (view == null) return;
            cellByCard.Remove(view);
            view.OnPointerClickEvent.RemoveListener(OnCellCardClicked);
            // Forward match → spin the card out (animator owns destroying it). Undo/revert → instant,
            // since a spin-out would misrepresent restoring the card. (DespawnAll also stays instant.)
            if (animate) removalAnimator.AnimateRemoval(view);
            else Destroy(view.gameObject);
        }

        private void OnCellCardClicked(UICard card)
        {
            if (card != null && cellByCard.TryGetValue(card, out var id)) cellTapped.OnNext(id);
        }

        private void RenderStock(BoardState state, bool canRecycle)
        {
            // Keep the stock slot visible/tappable while it holds cards OR a recycle is still available
            // (tapping the empty pile then recycles the waste).
            bool showStock = state.Stock.Count > 0 || canRecycle;
            if (showStock && stockCard == null && stockAnchor != null)
            {
                stockCard = Instantiate(cardPrefab, stockAnchor);
                stockCard.rectTransform.anchoredPosition = Vector2.zero;
                stockCard.IsDraggable = false;
                if (currentSpriteSet != null) stockCard.SetSpriteSet(currentSpriteSet);
                stockCard.SetCard(new PlayingCard(Rank.Ace, Suit.Spade)); // face hidden — any card; only the back shows
                stockCard.Close();
                stockCard.OnPointerClickEvent.AddListener(OnStockClicked);
            }
            if (stockCard != null) stockCard.SetVisible(showStock);
        }

        private void OnStockClicked(UICard _) => stockTapped.OnNext(Unit.Default);

        private void RenderWaste(BoardState state, bool animate, bool suppressSpawn = false)
        {
            int count = state.Waste.Count;
            // Waste-top removed by a match → fly the shown card out like a cell removal. A recycle also
            // empties the waste (RecycleCount changes), but it moves the cards to the stock rather than
            // removing them, so it must NOT spin out. (Undo/draw-reverse shrink too, but pass animate=false.)
            bool recycled = state.RecycleCount != lastRecycleCount;
            lastRecycleCount = state.RecycleCount;
            if (animate && !recycled && count < lastWasteCount && wasteCard != null)
            {
                wasteCard.OnPointerClickEvent.RemoveListener(OnWasteClicked);
                removalAnimator.AnimateRemoval(wasteCard);
                wasteCard = null;
            }
            lastWasteCount = count;
            if (suppressSpawn) return; // a play-to-waste flight is delivering+adopting the new waste card

            var top = state.WasteTop;
            if (top == null)
            {
                if (wasteCard != null) wasteCard.SetVisible(false);
                return;
            }
            if (wasteCard == null && wasteAnchor != null)
            {
                wasteCard = Instantiate(cardPrefab, wasteAnchor);
                wasteCard.rectTransform.anchoredPosition = Vector2.zero;
                wasteCard.IsDraggable = false;
                if (currentSpriteSet != null) wasteCard.SetSpriteSet(currentSpriteSet);
                wasteCard.OnPointerClickEvent.AddListener(OnWasteClicked);
            }
            if (wasteCard != null)
            {
                wasteCard.SetCard(top);
                wasteCard.OpenImmediate();
                wasteCard.SetVisible(true);
            }
        }

        private void OnWasteClicked(UICard _) => wasteTapped.OnNext(Unit.Default);

        private RectTransform AnchorFor(CellId id)
            => (cellAnchors != null && id.Value >= 0 && id.Value < cellAnchors.Length) ? cellAnchors[id.Value] : null;

        private void OnDestroy()
        {
            cellTapped.Dispose();
            stockTapped.Dispose();
            wasteTapped.Dispose();
        }
    }
}
