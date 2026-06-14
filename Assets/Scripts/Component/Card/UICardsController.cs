using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using R3;
using Component.Card.Events;
using Data.Card;
using Model.Card;
using Model.Game;
using NaughtyAttributes;
using Service.CardService;

namespace Component.Card
{
    /// <summary>
    /// UI representation of a placeholder with interactive capabilities.
    /// </summary>
    public class UICardsController : MonoBehaviour
    {
        // todo object pooling?
        [SerializeField]
        private UICard cardPrefab;

        private CardSpriteSet currentSpriteSet;

        [SerializeField]
        private RectTransform coverRootTransform;

        // Todo: Create card from pool.
        [SerializeField]
        private List<UICard> activeCards = new List<UICard>();

        [SerializeField]
        private List<UIPlaceHolder> placeholders;

        [SerializeField]
        private CardMoveAnimator moveAnimator;
        public CardMoveAnimator MoveAnimator => moveAnimator;

        private readonly Subject<CardDragStarted> onCardDragStartedSubject = new Subject<CardDragStarted>();
        private readonly Subject<CardDragCanceled> onCardDragCanceledSubject = new Subject<CardDragCanceled>();
        private readonly Subject<CardDroppedOnPile> onCardDroppedOnPileSubject = new Subject<CardDroppedOnPile>();
        private readonly Subject<CardClicked> onCardClickedSubject = new Subject<CardClicked>();
        private readonly Subject<PileId> onPlaceHolderClickedSubject = new Subject<PileId>();
        private readonly Dictionary<UICard, CardBindingInfo> cardBindingMap = new Dictionary<UICard, CardBindingInfo>();

        private CardBindingInfo activeDrag;
        private readonly List<CardBindingInfo> activeDragStack = new List<CardBindingInfo>();

        public Observable<CardDragStarted> OnCardDragStartedAsObservable() => onCardDragStartedSubject;
        public Observable<CardDragCanceled> OnCardDragCanceledAsObservable() => onCardDragCanceledSubject;
        public Observable<CardDroppedOnPile> OnCardDroppedOnPileAsObservable() => onCardDroppedOnPileSubject;
        public Observable<CardClicked> OnCardClickedAsObservable() => onCardClickedSubject;
        public Observable<PileId> OnPlaceHolderClickedAsObservable() => onPlaceHolderClickedSubject;

        private bool isDropResolved = false;

        private ICardService cardService;

        /// <summary>
        /// Wires the card service so pickup-run validation delegates to the rule source,
        /// not to a hardwired color check. Called by IngamePresenter after DI resolves ICardService.
        /// </summary>
        public void SetCardService(ICardService service) => cardService = service;

        private void Start()
        {
            EnsureCoverDoesNotBlockDrops();
            foreach (var placeHolder in placeholders)
            {
                SubscribePlaceHolder(placeHolder);
            }
        }

        /// <summary>The cover hosts the cards being dragged (rendered on top of the table). It must
        /// not block raycasts — otherwise a dragged card sits under the pointer and occludes the drop
        /// target beneath it, so OnDrop never reaches the destination pile and every drag-drop
        /// silently cancels. Enforced here (not per-prefab) so any table wired up stays drop-correct.</summary>
        private void EnsureCoverDoesNotBlockDrops()
        {
            if (coverRootTransform == null) return;
            // TryGetComponent, not `?? AddComponent`: GetComponent returns a Unity fake-null when the
            // component is absent, which `??` (reference equality) treats as non-null — so the add
            // would be skipped and the fake-null deref would throw MissingComponentException.
            if (!coverRootTransform.TryGetComponent<CanvasGroup>(out var coverGroup))
                coverGroup = coverRootTransform.gameObject.AddComponent<CanvasGroup>();
            coverGroup.blocksRaycasts = false;
        }

        private void OnDestroy()
        {
            if (placeholders != null)
            {
                foreach (var placeHolder in placeholders)
                {
                    UnsubscribePlaceHolder(placeHolder);
                }
            }

            if (activeCards != null)
            {
                foreach (var card in activeCards)
                {
                    UnsubscribeCard(card);
                }
            }

            foreach (var pair in cardBindingMap)
            {
                pair.Key.OnBeginDragEvent.RemoveAllListeners();
                pair.Key.OnEndDragEvent.RemoveAllListeners();
            }

            cardBindingMap.Clear();
            onCardDragStartedSubject.Dispose();
            onCardDragCanceledSubject.Dispose();
            onCardDroppedOnPileSubject.Dispose();
            onCardClickedSubject.Dispose();
            onPlaceHolderClickedSubject.Dispose();
        }

        private void OnDisable()
        {
            // Scene unload / controller disable: Unity will not deliver further
            // pointer events to this object, so an in-progress drag would be
            // left dangling. Restore any orphaned cards before the controller
            // goes idle.
            AbortLingeringDrag();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Losing window focus mid-drag (alt-tab, modal dialog, browser tab
            // switch in WebGL) causes Unity to stop sending OnDrag/OnEndDrag,
            // leaving the dragged cards orphaned under coverRoot. Cancel the
            // drag immediately so cards return to their pile.
            if (!hasFocus)
                AbortLingeringDrag();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // Mobile suspend / OS-level interrupt: same risk as focus loss.
            // Cancel any in-progress drag so the user finds their cards in a
            // sane state when they come back.
            if (pauseStatus)
                AbortLingeringDrag();
        }

        private void SubscribePlaceHolder(UIPlaceHolder placeHolder)
        {
            if (placeHolder == null) return;
            placeHolder.OnDropEvent.RemoveAllListeners();
            placeHolder.OnClickEvent.RemoveAllListeners();
            placeHolder.OnDropEvent.AddListener((p) => OnPlaceHolderDrop(p));
            placeHolder.OnClickEvent.AddListener((p) => OnPlaceHolderClick(p));
        }

        private void UnsubscribePlaceHolder(UIPlaceHolder placeHolder)
        {
            if (placeHolder == null) return;
            placeHolder.OnDropEvent.RemoveAllListeners();
            placeHolder.OnClickEvent.RemoveAllListeners();
        }

        [Button("Refresh Cards")]
        private void SubActiveCards()
        {
            if (activeCards != null)
            {
                foreach (var card in activeCards)
                {
                    UnsubscribeCard(card);
                }
            }

            if (activeCards != null)
            {
                foreach (var card in activeCards)
                {
                    SubscribeCard(card, default, default, card.rectTransform.parent);
                }
            }
        }

        public void SubscribeCard(UICard card, PileId sourcePileId, int sourceIndex, Transform parent)
        {
            if (card == null) return;

            cardBindingMap[card] = new CardBindingInfo(card, sourcePileId, sourceIndex, parent);
            card.SetPile(sourcePileId);
            card.OnBeginDragEvent.RemoveAllListeners();
            card.OnEndDragEvent.RemoveAllListeners();
            card.OnDropEvent.RemoveAllListeners();
            card.OnPointerClickEvent.RemoveAllListeners();
            card.OnBeginDragEvent.AddListener((c) => OnBeginDrag(c));
            card.OnEndDragEvent.AddListener((c) => OnEndDrag(c));
            card.OnDropEvent.AddListener((c) => OnCardDrop(c));
            card.OnPointerClickEvent.AddListener((c) => OnCardClick(c));
        }


        public void UnsubscribeCard(UICard card)
        {
            if (ReferenceEquals(card, null)) return;
            if (!cardBindingMap.ContainsKey(card)) return;

            if (card != null)
            {
                card.OnBeginDragEvent.RemoveAllListeners();
                card.OnEndDragEvent.RemoveAllListeners();
                card.OnDropEvent.RemoveAllListeners();
                card.OnPointerClickEvent.RemoveAllListeners();
            }

            cardBindingMap.Remove(card);
        }

        /// <summary>Sets the active skin and re-skins all currently spawned cards. Newly spawned cards use it too. Forwards to the move animator so ghost overlays use the same skin.</summary>
        public void ApplySpriteSet(CardSpriteSet spriteSet)
        {
            currentSpriteSet = spriteSet;
            if (moveAnimator != null) moveAnimator.ApplySpriteSet(spriteSet);
            if (spriteSet == null) return;
            foreach (var card in activeCards)
            {
                if (card != null) card.SetSpriteSet(spriteSet);
            }
        }

        // Todo: Pooling
        public UICard SpawnCard(PlayingCard card, PileId pileId, int index)
        {
            var placeholder = GetPlaceholder(pileId);
            var anchor = placeholder != null && placeholder.Anchor != null ? placeholder.Anchor : transform;
            var offset = placeholder != null ? placeholder.CardOffset : Vector2.zero;

            var uiCard = Instantiate(cardPrefab, anchor);
            if (currentSpriteSet != null) uiCard.SetSpriteSet(currentSpriteSet);
            uiCard.SetCard(card);
            uiCard.rectTransform.anchoredPosition = offset * index;

            activeCards.Add(uiCard);
            SubscribeCard(uiCard, pileId, index, anchor);
            return uiCard;
        }

        public void DespawnAllCards()
        {
            foreach (var card in activeCards)
            {
                UnsubscribeCard(card);
                if (card != null) Destroy(card.gameObject);
            }
            activeCards.Clear();
            // cardBindingMap must be explicitly cleared here. UnsubscribeCard skips
            // null (destroyed) Unity objects, so any cards destroyed by prior cascade
            // would leave stale entries without this call.
            cardBindingMap.Clear();
        }

        public void DespawnPile(PileId pileId)
        {
            var toRemove = new List<UICard>();
            foreach (var card in activeCards)
                if (cardBindingMap.TryGetValue(card, out var b) && b.SourcePileId.Equals(pileId))
                    toRemove.Add(card);

            foreach (var card in toRemove)
            {
                UnsubscribeCard(card);
                if (card != null) Destroy(card.gameObject);
                activeCards.Remove(card);
            }
        }

        public void SetPlaceholderRestoreVisible(PileId pileId, bool visible)
        {
            var placeholder = GetPlaceholder(pileId);
            if (placeholder != null) placeholder.SetRestoreVisible(visible);
        }

        private UIPlaceHolder GetPlaceholder(PileId pileId) =>
            placeholders.FirstOrDefault(p => p.PileId.Type == pileId.Type && p.PileId.Index == pileId.Index);

        /// <summary>
        /// Returns the world position of a card at the given pile and index.
        /// If the card is already spawned, its actual position is used (handles custom
        /// positioning such as waste fan in Draw-3 mode). Otherwise falls back to
        /// placeholder offset calculation.
        /// </summary>
        /// <remarks>
        /// The fallback path uses <c>cardOffset * index</c>, which does not account
        /// for fan positioning. This is acceptable because:
        /// <list type="bullet">
        ///   <item>Move animations call this AFTER render, so target cards are already spawned.</item>
        ///   <item>Stock↔Waste moves skip ghost animation entirely.</item>
        ///   <item>Hint previews use source/target from current state where cards exist.</item>
        /// </list>
        /// If future code paths need fanned positions for not-yet-spawned cards,
        /// the fan offset calculation should be extracted and shared.
        /// </remarks>
        public Vector3 GetCardWorldPosition(PileId pileId, int index)
        {
            var card = FindCard(pileId, index);
            if (card != null)
                return card.rectTransform.position;

            var placeholder = GetPlaceholder(pileId);
            if (placeholder == null || placeholder.Anchor == null) return transform.position;

            var anchor = placeholder.Anchor;
            var localOffset = placeholder.CardOffset * index;
            return anchor.TransformPoint(localOffset);
        }

        /// <summary>
        /// Finds the UICard at the given pile and index. Returns null if not found.
        /// </summary>
        public UICard FindCard(PileId pileId, int index)
        {
            foreach (var card in activeCards)
            {
                if (cardBindingMap.TryGetValue(card, out var b)
                    && b.SourcePileId.Equals(pileId)
                    && b.SourceIndex == index)
                    return card;
            }
            return null;
        }

        /// <summary>
        /// Returns every active UICard whose source pile is a Foundation, in pile/index order
        /// (lowest pile index first, then lowest card index first).
        /// </summary>
        public IReadOnlyList<UICard> GetFoundationCards()
        {
            var result = new List<UICard>();
            foreach (var card in activeCards)
            {
                if (card == null) continue;
                if (cardBindingMap.TryGetValue(card, out var b)
                    && b.SourcePileId.Type == PileType.Foundation)
                    result.Add(card);
            }
            result.Sort((a, b) =>
            {
                cardBindingMap.TryGetValue(a, out var ba);
                cardBindingMap.TryGetValue(b, out var bb);
                int pileCmp = ba.SourcePileId.Index.CompareTo(bb.SourcePileId.Index);
                return pileCmp != 0 ? pileCmp : ba.SourceIndex.CompareTo(bb.SourceIndex);
            });
            return result;
        }

        /// <summary>
        /// Highlights the source card(s) and target pile/placeholder for a hint move.
        /// </summary>
        public void ShowHintHighlight(PileId sourcePileId, int sourceIndex, PileId targetPileId)
        {
            ClearHintHighlight();

            // Highlight source card(s) — all cards at sourceIndex and above in the source pile
            foreach (var card in activeCards)
            {
                if (cardBindingMap.TryGetValue(card, out var b)
                    && b.SourcePileId.Equals(sourcePileId)
                    && b.SourceIndex >= sourceIndex)
                {
                    card.SetHighlight(true);
                }
            }

            // Highlight target — either a card on top of target pile, or the placeholder if empty
            UICard topTargetCard = null;
            int topIndex = -1;
            foreach (var card in activeCards)
            {
                if (cardBindingMap.TryGetValue(card, out var b)
                    && b.SourcePileId.Equals(targetPileId)
                    && b.SourceIndex > topIndex)
                {
                    topTargetCard = card;
                    topIndex = b.SourceIndex;
                }
            }

            if (topTargetCard != null)
            {
                topTargetCard.SetHighlight(true);
            }
            else
            {
                var placeholder = GetPlaceholder(targetPileId);
                if (placeholder != null) placeholder.SetHighlight(true);
            }
        }

        /// <summary>
        /// Clears all hint highlights from cards and placeholders.
        /// </summary>
        public void ClearHintHighlight()
        {
            foreach (var card in activeCards)
            {
                if (card != null) card.SetHighlight(false);
            }

            foreach (var placeholder in placeholders)
            {
                if (placeholder != null) placeholder.SetHighlight(false);
            }
        }

        // // Detach one card or a stack from a Pile
        // public List<UICard> DetachFromPile(PileId pileId, int fromIndex)
        // {
        //     var pile = pileCards[pileId];
        //     var detached = pile.GetRange(fromIndex, pile.Count - fromIndex);
        //     pile.RemoveRange(fromIndex, pile.Count - fromIndex);
        //     // Remaining cards in the source pile stay in place — no additional update needed
        //     return detached;
        // }

        // // Attach one card or a stack to the end of a Pile
        // public void AttachToPile(PileId pileId, List<UICard> cards)
        // {
        //     var pile = pileCards[pileId];
        //     var anchor = pileAnchors[pileId];
        //     int startIndex = pile.Count;

        //     foreach (var card in cards)
        //     {
        //         card.rectTransform.SetParent(anchor, false);
        //         card.rectTransform.anchoredPosition = new Vector2(0, -startIndex * cardOffset);
        //         pile.Add(card);
        //         startIndex++;
        //     }
        // }

        #region Event Handlers
        private void OnBeginDrag(UICard card)
        {
            if (card == null) return;
            if (!cardBindingMap.TryGetValue(card, out var binding)) return;
            Debug.Log($"OnBeginDrag: {binding}");

            // Previous drag's OnEndDrag may have been lost — restore orphaned cards.
            AbortLingeringDrag();

            // Gather all cards in the same pile at or above the dragged card's index (stack drag)
            activeDragStack.Clear();
            foreach (var c in activeCards)
            {
                if (cardBindingMap.TryGetValue(c, out var b)
                    && b.SourcePileId.Equals(binding.SourcePileId)
                    && b.SourceIndex >= binding.SourceIndex)
                    activeDragStack.Add(b);
            }
            activeDragStack.Sort((a, b) => a.SourceIndex.CompareTo(b.SourceIndex));

            // Tableau multi-card pickup: the run shape check lives in the card service,
            // not here — that way the rule (Klondike alternating vs Spider same-suit)
            // is a single source of truth. Veto the gesture so EventSystem suppresses
            // OnDrag/OnEndDrag — otherwise the lead card would slide under the pointer
            // while the stack stays put.
            if (binding.SourcePileId.Type == PileType.Tableau
                && activeDragStack.Count > 1
                && !IsValidPickup(activeDragStack))
            {
                activeDragStack.Clear();
                card.RejectDrag();
                return;
            }

            isDropResolved = false;
            activeDrag = binding;

            // Save drag state for all stack cards BEFORE reparenting
            foreach (var b in activeDragStack)
                b.Card.SaveDragStartState();

            // Lead card goes to coverRoot; remaining stack cards become children of the lead
            // so they follow the drag automatically as the lead card moves via OnDrag
            card.rectTransform.SetParent(coverRootTransform, true);
            for (int i = 1; i < activeDragStack.Count; i++)
                activeDragStack[i].Card.rectTransform.SetParent(card.rectTransform, true);

            onCardDragStartedSubject.OnNext(new CardDragStarted(binding.Card, binding.Card.GetCard(), binding.SourcePileId, binding.SourceIndex));
        }

        /// <summary>Delegates the pickup-run check to the card service — the run rule (Klondike
        /// alternating vs Spider same-suit) lives there, never client-side.</summary>
        private bool IsValidPickup(List<CardBindingInfo> stack)
        {
            var run = new List<PlayingCard>(stack.Count);
            foreach (var b in stack)
            {
                var card = b.Card.GetCard();
                if (card == null) return false; // mid-despawn — veto the gesture
                run.Add(card);
            }
            if (cardService == null)
            {
                // A wiring gap (SetCardService never called) would otherwise present as
                // silent dead drags — make it diagnosable.
                Debug.LogWarning("[UICardsController] cardService not wired — multi-card drag vetoed.");
                return false;
            }
            return cardService.IsValidRunPickup(run);
        }

        private void OnEndDrag(UICard card)
        {
            Debug.Log($"OnEndDrag: {card.name}");
            if (activeDrag.Card == null) return;

            if (!isDropResolved)
            {
                foreach (var b in activeDragStack)
                    b.Card.RestoreToDragStartPosition();

                onCardDragCanceledSubject.OnNext(new CardDragCanceled(activeDrag.Card, activeDrag.Card.GetCard(), activeDrag.SourcePileId, activeDrag.SourceIndex));
            }

            activeDrag = default;
            activeDragStack.Clear();
        }

        /// <summary>
        /// Restores orphaned cards from a missed OnEndDrag back to their source pile.
        /// Triggered by new BeginDrag, focus loss, or controller disable.
        /// </summary>
        private void AbortLingeringDrag()
        {
            if (activeDrag.Card == null && activeDragStack.Count == 0) return;

            Debug.LogWarning(
                $"[UICardsController] Lingering drag detected — restoring orphaned cards. " +
                $"Source pile: {activeDrag.SourcePileId} index: {activeDrag.SourceIndex}");

            var canceledBinding = activeDrag;

            bool leadRestored = false;

            foreach (var b in activeDragStack)
            {
                if (b.Card == null) continue;
                b.Card.RestoreToDragStartPosition();
                if (b.Card == canceledBinding.Card)
                    leadRestored = true;
            }

            // Fallback restore for the lead card when the stack didn't cover it.
            if (!leadRestored && canceledBinding.Card != null)
                canceledBinding.Card.RestoreToDragStartPosition();

            activeDrag = default;
            activeDragStack.Clear();
            isDropResolved = false;

            if (canceledBinding.Card != null)
            {
                onCardDragCanceledSubject.OnNext(new CardDragCanceled(
                    canceledBinding.Card,
                    canceledBinding.Card.GetCard(),
                    canceledBinding.SourcePileId,
                    canceledBinding.SourceIndex));
            }
        }

        private void OnPlaceHolderDrop(UIPlaceHolder placeHolder)
        {
            if (placeHolder == null) return;
            if (activeDrag.Card == null) return;

            isDropResolved = true;
            Debug.Log($"OnPlaceHolderDrop: {placeHolder.name}");
            onCardDroppedOnPileSubject.OnNext(new CardDroppedOnPile(
                activeDrag.Card,
                activeDrag.Card.GetCard(),
                activeDrag.SourcePileId,
                activeDrag.SourceIndex,
                placeHolder.PileId,
                activeDragStack.Count));
        }

        private void OnCardDrop(UICard card)
        {
            if (card == null) return;
            if (activeDrag.Card == null) return;

            Debug.Log($"OnCardDrop: {card.name}");
            onCardDroppedOnPileSubject.OnNext(new CardDroppedOnPile(
                activeDrag.Card,
                activeDrag.Card.GetCard(),
                activeDrag.SourcePileId,
                activeDrag.SourceIndex,
                card.PileId,
                activeDragStack.Count));
        }

        private void OnCardClick(UICard card)
        {
            if (card == null) return;
            if (!cardBindingMap.TryGetValue(card, out var binding)) return;
            onCardClickedSubject.OnNext(new CardClicked(binding.Card, binding.Card.GetCard(), binding.SourcePileId, binding.SourceIndex));
        }

        private void OnPlaceHolderClick(UIPlaceHolder placeHolder)
        {
            if (placeHolder == null) return;
            onPlaceHolderClickedSubject.OnNext(placeHolder.PileId);
        }
        #endregion

        private readonly struct CardBindingInfo
        {
            public UICard Card { get; }
            public PileId SourcePileId { get; }
            public int SourceIndex { get; }
            public Transform SourceTransform { get; }

            public CardBindingInfo(UICard card, PileId sourcePileId, int sourceIndex, Transform sourceTransform)
            {
                Card = card;
                SourcePileId = sourcePileId;
                SourceIndex = sourceIndex;
                SourceTransform = sourceTransform;
            }

            public override string ToString()
            {
                return $"CardBindingInfo(Card: {Card.name}, SourcePileId: {SourcePileId}, SourceIndex: {SourceIndex})";
            }
        }
    }
}
