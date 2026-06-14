using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Model.Game;
using NaughtyAttributes;

namespace Component.Card
{
    /// <summary>
    /// UI representation of a placeholder with interactive capabilities.
    /// </summary>
    public class UIPlaceHolder : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        [SerializeField]
        private RectTransform anchor;
        public RectTransform Anchor => anchor;

        /// <summary>Per-card stacking offset (anchoredPosition = cardOffset * index). Set per pile type in Inspector.</summary>
        [SerializeField]
        private Vector2 cardOffset;
        public Vector2 CardOffset => cardOffset;


        [ValidateInput("IsPileNone", "PileId must be initialized with a valid PileType")]
        [SerializeField]
        /// <summary>
        /// Indicates the type of pile this placeholder represents (e.g., tableau, foundation, stock).
        /// </summary>
        private PileType pileType;

        [SerializeField]
        private int pileIndex;

        /// <summary>
        /// The unique identifier of the pile this placeholder represents, combining pile type, index, and optionally the top card.
        /// </summary>
        public PileId PileId { private set; get; }


        [SerializeField]
        private GameObject restoreVisual;

        [SerializeField]
        private GameObject highlightVisual;

        // UnityEvent backing fields (serializable, inspector-visible)
        [SerializeField]
        public UnityEvent<UIPlaceHolder> OnDropEvent;
        public UnityEvent<UIPlaceHolder> OnClickEvent;


        private void Awake()
        {
            // Initialize pileId with a placeholder card for empty piles
            PileId = new PileId(pileType, pileIndex);
        }

        private bool IsPileNone(PileType type) => type != PileType.None;

        public void SetRestoreVisible(bool visible)
        {
            if (restoreVisual != null) restoreVisual.SetActive(visible);
        }

        /// <summary>
        /// Toggles the hint highlight visual on this placeholder.
        /// </summary>
        public void SetHighlight(bool active)
        {
            if (highlightVisual != null) highlightVisual.SetActive(active);
        }


        #region Event Handlers
        public void OnDrop(PointerEventData eventData)
        {
            OnDropEvent?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClickEvent?.Invoke(this);
        }
        #endregion
    }
}
