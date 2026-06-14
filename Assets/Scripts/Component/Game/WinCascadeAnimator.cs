using System;
using System.Collections.Generic;
using System.Threading;
using Component.Card;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Component.Game
{
    /// <summary>Win celebration cascade. Stagger-fires `CardMoveAnimator.AnimateMove` on every
    /// foundation card, sending each ghost spraying down across the play area. Real cards
    /// stay in place — the existing ghost system already restores state on completion.</summary>
    public class WinCascadeAnimator : MonoBehaviour
    {
        [SerializeField] private UICardsController cardsController;

        /// <summary>Retargets the cascade to a different controller (called on game-type switch).</summary>
        public void SetController(UICardsController c) => cardsController = c;

        [Header("Cascade")]
        // Per-ghost flight duration. CardMoveAnimator's default 0.2s is tuned for short pile-to-pile
        // moves; at flyDistance 2000 that translates to 10000 units/sec, pushing every ghost
        // off-screen in ~2 frames. The cascade overrides with a slower value so the player can
        // actually see the celebration arc.
        [SerializeField, Min(0.05f)] private float cascadeDuration = 1.2f;
        [SerializeField, Min(0f)] private float perCardStaggerSeconds = 0.05f;
        // World-unit distance, not pixels. The canvas hierarchy applies a small lossy scale
        // (~0.005) so a value of ~10 already traverses roughly one screen height with the
        // downward fan below; values like 2000 zip ghosts hundreds of screens away in a single
        // frame. Tune in the scene instance for visual taste.
        [SerializeField, Min(0f)] private float flyDistance = 10f;
        // Downward fan — 0° = right, 90° = up, 180° = left, 270° = down. Foundation lives at
        // the top of the play area, so flying upward (60°–120°) just exits the screen instantly.
        // 240°–300° spray cards down across the tableau where they're actually visible.
        [SerializeField, Range(0f, 360f)] private float minAngleDegrees = 240f;
        [SerializeField, Range(0f, 360f)] private float maxAngleDegrees = 300f;

        public async UniTask PlayAsync(CancellationToken ct = default)
        {
            if (cardsController == null) return;

            // Each table owns its CardMoveAnimator; source it from the active controller.
            // The scene-level animator was removed in the per-table refactor, so a serialized
            // field here would always be null — read the live one off the active controller.
            var moveAnimator = cardsController.MoveAnimator;
            if (moveAnimator == null) return;

            var cards = cardsController.GetFoundationCards();
            if (cards == null || cards.Count == 0) return;

            // Link the caller's token with this MonoBehaviour's destroy token so a scene unload
            // (or animator destruction) cancels in-flight delays even if the caller passed default.
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                ct, this.GetCancellationTokenOnDestroy());
            var linkedCt = linkedCts.Token;

            var stagger = TimeSpan.FromSeconds(perCardStaggerSeconds);
            var inFlight = new List<UniTask>(cards.Count);

            for (int i = 0; i < cards.Count; i++)
            {
                if (linkedCt.IsCancellationRequested) break;
                inFlight.Add(AnimateOneAsync(moveAnimator, cards[i], linkedCt));
                if (i < cards.Count - 1)
                    await UniTask.Delay(stagger, cancellationToken: linkedCt);
            }

            await UniTask.WhenAll(inFlight);
        }

        private UniTask AnimateOneAsync(CardMoveAnimator moveAnimator, UICard card, CancellationToken ct)
        {
            // Skip null refs and cards that were destroyed (Unity's null-overload returns true for
            // missing-but-not-null refs) so we don't dereference rectTransform on a dead GameObject.
            if (card == null || card.rectTransform == null) return UniTask.CompletedTask;

            var fromWorld = card.rectTransform.position;
            var angleRad = UnityEngine.Random.Range(minAngleDegrees, maxAngleDegrees) * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);
            var toWorld = fromWorld + direction * flyDistance;

            var tcs = new UniTaskCompletionSource();
            moveAnimator.AnimateMove(card.GetCard(), faceUp: true, fromWorld, toWorld,
                cascadeDuration, () => tcs.TrySetResult());
            return tcs.Task.AttachExternalCancellation(ct);
        }
    }
}
