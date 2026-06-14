using Component.Card;
using Component.Card.Events;
using Component.Game;
using Cysharp.Threading.Tasks;
using Data.Card;
using Model.Card;
using Model.Game;
using R3;
using Service.CardService;
using System.Threading;
using UnityEngine;

namespace Scene.Ingame
{
    public class IngameComponent : MonoBehaviour
    {
        [SerializeField] private UICardsController klondikeTable;
        [SerializeField] private UICardsController spiderTable;
        [SerializeField] private WinCascadeAnimator winCascadeAnimator;

        private CardViewSet cards;
        private UICardsController active;
        private ICardService pendingCardService;

        private void Awake()
        {
            cards = new CardViewSet(klondikeTable, spiderTable);
            // Default active so forwarder calls before the first ActivateLayout don't NPE (risk R1).
            active = klondikeTable;
        }

        public UniTask PlayWinCascadeAsync(CancellationToken ct = default)
            => winCascadeAnimator != null ? winCascadeAnimator.PlayAsync(ct) : UniTask.CompletedTask;

        /// <summary>Selects the controller for the given game type, toggles table visibility,
        /// retargets the win cascade animator, and forwards a pending card service if set.</summary>
        public void ActivateLayout(GameType gameType)
        {
            active = cards.For(gameType);
            // SetActive(false) fires OnDisable→AbortLingeringDrag on the outgoing table; safe here
            // because ActivateLayout only runs at init (table idle, no drag in progress).
            foreach (var c in cards.All)
                c.gameObject.SetActive(c == active);
            if (pendingCardService != null)
                active.SetCardService(pendingCardService);
            if (winCascadeAnimator != null)
                winCascadeAnimator.SetController(active);
        }

        /// <summary>Forwards the card service to the active controller. Stored so it can be
        /// re-applied when ActivateLayout switches to a different controller.</summary>
        public void SetCardService(ICardService cardService)
        {
            pendingCardService = cardService;
            if (active != null)
                active.SetCardService(cardService);
        }

        // --- Hint Highlight ---

        public void ShowHintHighlight(PileId sourcePileId, int sourceIndex, PileId targetPileId)
            => active.ShowHintHighlight(sourcePileId, sourceIndex, targetPileId);

        public void ClearHintHighlight()
            => active.ClearHintHighlight();

        // --- Card Move Animation ---

        public CardMoveAnimator MoveAnimator => active?.MoveAnimator;

        public Vector3 GetCardWorldPosition(PileId pileId, int index)
            => active.GetCardWorldPosition(pileId, index);

        public UICard FindCard(PileId pileId, int index)
            => active.FindCard(pileId, index);

        // --- Card Lifecycle ---

        public UICard SpawnCard(PlayingCard card, PileId pileId, int index)
            => active.SpawnCard(card, pileId, index);

        /// <summary>Applies the sprite set to ALL controllers so each stays skinned on activate.</summary>
        public void ApplySpriteSet(CardSpriteSet spriteSet)
        {
            foreach (var c in cards.All)
                c.ApplySpriteSet(spriteSet);
        }

        /// <summary>Despawns cards on ALL controllers — a game-type switch activates the new
        /// table before despawning, so clearing only `active` would orphan the outgoing table's cards.</summary>
        public void DespawnAllCards()
        {
            foreach (var c in cards.All)
                c.DespawnAllCards();
        }

        public void DespawnPile(PileId pileId)
            => active.DespawnPile(pileId);

        public void SetStockRestoreVisible(bool visible)
        {
            active.SetPlaceholderRestoreVisible(new PileId(PileType.Stock, 0), visible);
        }

        public void RevertCardDrop(UICard card)
        {
            if (card == null) return;
            card.RestoreToDragStartPosition();
        }

        public void MoveCardToPile(UICard card, Transform targetPileTransform)
        {
            if (card == null || targetPileTransform == null) return;
            card.rectTransform.SetParent(targetPileTransform, true);
        }

        // --- Card Events ---
        // Merge both tables' streams: the presenter subscribes once at Start (before the first
        // ActivateLayout), so binding to `active` would lock onto the wrong table for a Spider
        // game. The inactive table's GameObject receives no input, so it never emits — no crosstalk.

        public Observable<CardDragStarted> OnCardDragStartedAsObservable()
            => Observable.Merge(klondikeTable.OnCardDragStartedAsObservable(), spiderTable.OnCardDragStartedAsObservable());

        public Observable<CardDroppedOnPile> OnCardDroppedOnPileAsObservable()
            => Observable.Merge(klondikeTable.OnCardDroppedOnPileAsObservable(), spiderTable.OnCardDroppedOnPileAsObservable());

        public Observable<CardDragCanceled> OnCardDragCanceledAsObservable()
            => Observable.Merge(klondikeTable.OnCardDragCanceledAsObservable(), spiderTable.OnCardDragCanceledAsObservable());

        public Observable<CardClicked> OnCardClickedAsObservable()
            => Observable.Merge(klondikeTable.OnCardClickedAsObservable(), spiderTable.OnCardClickedAsObservable());

        public Observable<PileId> OnPlaceHolderClickedAsObservable()
            => Observable.Merge(klondikeTable.OnPlaceHolderClickedAsObservable(), spiderTable.OnPlaceHolderClickedAsObservable());

        public void BindCard(UICard card, PileId sourcePileId, int sourceIndex, Transform parent)
            => active.SubscribeCard(card, sourcePileId, sourceIndex, parent);

        public void UnbindCard(UICard card)
            => active.UnsubscribeCard(card);
    }
}
