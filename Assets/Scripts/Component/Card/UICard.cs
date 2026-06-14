using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Model.Card;
using NaughtyAttributes;
using Model.Game;
using Data.Card;

namespace Component.Card
{
    /// <summary>
    /// UI representation of a playing card with interactive capabilities.
    /// </summary>
    public class UICard : MonoBehaviour,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler
    {
        private PlayingCard cardData;
        [SerializeField]
        private Animator animator;
        [SerializeField]
        private CanvasGroup canvasGroup;
        [SerializeField]
        private Image frontImage;
        [SerializeField]
        private Image backImage;
        [SerializeField]
        private CardSpriteSet cardSpriteSet;
        [SerializeField]
        private GameObject highlightVisual;
        [SerializeField]
        private GameObject coverVisual;

        [Required]
        public RectTransform rectTransform;
        private Transform dragOriginParent;
        private Vector2 dragStartAnchoredPosition;
        private Vector3 dragPointerOffset;

        public PileId PileId { set; get; }

        private static readonly int animatorHashKeyIsOpen = Animator.StringToHash("IsOpen");
        private static readonly int animatorHashKeySetOpen = Animator.StringToHash("SetOpen");
        private static readonly int animatorHashKeySetClose = Animator.StringToHash("SetClose");
        /// <summary>
        /// Indicates whether the card is currently open (face shown).
        /// </summary>
        public bool IsOpen { get; private set; }
        public bool IsDraggable { get; set; } = true;

        // Set to false by a drag subscriber (e.g. UICardsController) during OnBeginDrag to
        // abort the gesture before OnDrag fires. Restored automatically each begin attempt.
        private bool dragAccepted = true;
        private bool shaking; // guards against overlapping shake animations

        /// <summary>Subscribers call this inside OnBeginDragEvent to veto the pickup.</summary>
        public void RejectDrag() => dragAccepted = false;


        // UnityEvent backing fields (serializable, inspector-visible)
        public UnityEvent<UICard> OnPointerClickEvent;
        public UnityEvent<UICard> OnBeginDragEvent;
        public UnityEvent<Vector2> OnDragEvent;
        public UnityEvent<UICard> OnEndDragEvent;
        public UnityEvent<UICard> OnDropEvent;


        [Button("Set Random Dummy Card Data")]
        public void SetRandomDummyCardData()
        {
            var randomSuit = (Suit)Random.Range(1, 5);
            var randomRank = (Rank)Random.Range(1, 14);
            SetCard(new PlayingCard(randomRank, randomSuit));
            SetPile(new PileId((PileType)Random.Range(1, 5), 0));
        }

        /// <summary>
        /// Sets the playing card data for this UI card.
        /// </summary>
        /// <param name="card">The playing card data to set.</param>
        public void SetCard(PlayingCard card)
        {
            this.cardData = card;
            RefreshCardSprites();
        }

        /// <summary>
        /// Gets the playing card domain model for this UI card.
        /// </summary>
        /// <returns>
        /// The corresponding <see cref="PlayingCard"/> instance if card data has been set; otherwise, <c>null</c>.
        /// </returns>
        public PlayingCard GetCard() => cardData;

        public void SetPile(PileId newPileId)
        {
            PileId = newPileId;
        }

        public void SetSpriteSet(CardSpriteSet spriteSet)
        {
            cardSpriteSet = spriteSet;
            RefreshCardSprites();
        }

        [Button("Refresh Card Sprites")]
        public void RefreshCardSprites()
        {
            if (cardSpriteSet == null)
            {
                return;
            }

            if (backImage != null)
            {
                backImage.sprite = cardSpriteSet.BackSprite;
            }

            if (frontImage != null && cardData != null && cardSpriteSet.TryGetFrontSprite(cardData, out var frontSprite))
            {
                frontImage.sprite = frontSprite;
            }
        }

        public void RestoreToDragStartPosition()
        {
            rectTransform.SetParent(dragOriginParent, true);
            rectTransform.anchoredPosition = dragStartAnchoredPosition;
        }

        /// <summary>
        /// Manually saves the current parent and anchored position as the drag start state.
        /// Call this on stack cards before reparenting them during a batch drag.
        /// </summary>
        public void SaveDragStartState()
        {
            dragOriginParent = rectTransform.parent;
            dragStartAnchoredPosition = rectTransform.anchoredPosition;
        }

        /// <summary>
        /// Opens the card (shows face) with flip animation.
        /// </summary>
        public void Open()
        {
            if (IsOpen) return;

            IsOpen = true;
            if (animator != null) animator.SetBool(animatorHashKeyIsOpen, true);
        }

        /// <summary>
        /// Opens the card (shows face) instantly, skipping the flip animation.
        /// Use when spawning cards that should already be face-up (e.g. target pile re-render).
        /// </summary>
        public void OpenImmediate()
        {
            if (IsOpen) return;

            IsOpen = true;
            if (animator == null) return;

            animator.SetBool(animatorHashKeyIsOpen, true);
            animator.SetTrigger(animatorHashKeySetOpen);
        }

        /// <summary>
        /// Enables interaction with the card.
        /// </summary>
        [Button("Enable Interaction")]
        public void Enable()
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// Disables interaction with the card.
        /// </summary>
        [Button("Disable Interaction")]
        public void Disable()
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// Sets card visibility via CanvasGroup alpha.
        /// Used to hide the real card while a ghost overlay is animating to the same position.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            if (!visible)
                canvasGroup.blocksRaycasts = false;
            else
                canvasGroup.blocksRaycasts = canvasGroup.interactable;
        }

        /// <summary>Sets the card's alpha (0–1) for fade effects, using the same CanvasGroup as SetVisible.</summary>
        public void SetAlpha(float alpha)
        {
            if (canvasGroup != null) canvasGroup.alpha = alpha;
        }

        /// <summary>
        /// Closes the card (shows back).
        /// </summary>
        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            if (animator != null) animator.SetBool(animatorHashKeyIsOpen, false);
        }

        /// <summary>
        /// Opens the card (shows face) instantly, skipping the flip animation.
        /// Use when spawning cards that should already be face-up (e.g. target pile re-render).
        /// </summary>
        public void CloseImmediate()
        {
            if (!IsOpen) return;

            IsOpen = false;
            if (animator != null) animator.SetTrigger(animatorHashKeySetClose);
        }

        /// <summary>
        /// Toggles the dark "locked" cover overlay (board games darken covered cards).
        /// </summary>
        public void SetCovered(bool covered)
        {
            if (coverVisual != null) coverVisual.SetActive(covered);
        }

        /// <summary>
        /// Toggles the hint highlight visual on this card.
        /// </summary>
        public void SetHighlight(bool active)
        {
            if (highlightVisual != null) highlightVisual.SetActive(active);
        }

        /// <summary>Brief horizontal shake for invalid-move feedback. Restores the original position;
        /// changes no card state. Ignores the call if a shake is already running.</summary>
        public void Shake(float amplitude = 16f, float duration = 0.3f)
        {
            if (shaking || rectTransform == null) return;
            ShakeAsync(amplitude, duration).Forget();
        }

        private async UniTaskVoid ShakeAsync(float amplitude, float duration)
        {
            shaking = true;
            Vector2 origin = rectTransform.anchoredPosition;
            try
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    if (rectTransform == null) return;
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    // ~3 damped oscillations, amplitude decaying to 0
                    float offset = Mathf.Sin(t * Mathf.PI * 6f) * amplitude * (1f - t);
                    rectTransform.anchoredPosition = origin + new Vector2(offset, 0f);
                    await UniTask.Yield();
                }
            }
            finally
            {
                if (rectTransform != null) rectTransform.anchoredPosition = origin;
                shaking = false;
            }
        }

        #region Event Handlers
        public void OnPointerClick(PointerEventData eventData)
        {
            // A drag gesture also delivers OnPointerClick — Unity keeps eligibleForClick true and
            // fires the click before OnEndDrag. Without this guard a drag would also run the tap
            // path: the tap-reject shake captures the card's drag-release position and re-pins it
            // there every frame, overriding the drag-cancel restore (card stranded where dropped).
            if (eventData.dragging) return;
            OnPointerClickEvent?.Invoke(this);
        }

        // Resolved once per drag in OnBeginDrag (Camera.main does a tagged scene lookup) and reused
        // in OnDrag, which fires every frame — avoids re-resolving the camera per frame.
        private Camera dragCamera;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsDraggable)
            {
                eventData.pointerDrag = null;
                return;
            }

            dragOriginParent = rectTransform.parent;
            dragStartAnchoredPosition = rectTransform.anchoredPosition;

            dragCamera = Camera.main;
            if (dragCamera == null)
            {
                // No active MainCamera — can't map screen→world; cancel the drag cleanly.
                eventData.pointerDrag = null;
                return;
            }
            var pointerWorldPos = dragCamera.ScreenToWorldPoint(eventData.position);
            pointerWorldPos.z = rectTransform.position.z;
            dragPointerOffset = rectTransform.position - pointerWorldPos;

            dragAccepted = true;
            OnBeginDragEvent?.Invoke(this);

            if (!dragAccepted)
            {
                // Subscriber vetoed (e.g. invalid sequence). Suppress OnDrag/OnEndDrag from EventSystem.
                eventData.pointerDrag = null;
                dragAccepted = true;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDraggable) return;
            if (dragCamera == null) return;

            OnDragEvent?.Invoke(eventData.position);

            var newPosition = dragCamera.ScreenToWorldPoint(eventData.position);
            newPosition.z = rectTransform.position.z;
            rectTransform.position = newPosition + dragPointerOffset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDraggable) return;
            OnEndDragEvent?.Invoke(this);
        }

        public void OnDrop(PointerEventData eventData)
        {
            OnDropEvent?.Invoke(this);
        }
        #endregion
    }
}
