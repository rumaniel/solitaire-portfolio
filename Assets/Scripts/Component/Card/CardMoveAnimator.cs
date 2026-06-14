using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data.Card;
using Model.Card;
using UnityEngine;

namespace Component.Card
{
    /// <summary>
    /// Handles ghost card animations for move visualization and hint preview.
    /// Ghost cards are non-interactive visual overlays parented under ghostRoot (coverRootTransform).
    /// Real cards are never touched — fully interrupt-safe.
    /// </summary>
    public class CardMoveAnimator : MonoBehaviour
    {
        [SerializeField] private UICard ghostPrefab;
        [SerializeField] private RectTransform ghostRoot;

        [Header("Move Animation")]
        [SerializeField] private float moveDuration = 0.2f;
        [SerializeField] private AnimationCurve moveEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Reject Shake")]
        [SerializeField] private float shakeDuration = 0.3f;
        [SerializeField] private float shakeIntensity = 8f;
        [SerializeField] private int shakeOscillations = 3;

        [Header("Hint Preview")]
        [SerializeField] private float previewOutDuration = 0.25f;
        [SerializeField] private float previewFadeDuration = 0.15f;
        [SerializeField] private float previewGhostAlpha = 0.8f;

        private CancellationTokenSource masterCts;
        private readonly List<GameObject> activeGhosts = new();
        private CardSpriteSet currentSpriteSet;

        /// <summary>Updates the sprite set applied to future ghost cards. Live ghosts are not retroactively re-skinned (they're transient — next animation uses the new set).</summary>
        public void ApplySpriteSet(CardSpriteSet spriteSet) => currentSpriteSet = spriteSet;

        /// <summary>Per-target shake state so re-taps on the same card don't drift the origin.</summary>
        private sealed class ShakeState
        {
            public CancellationTokenSource Cts;
            public Vector2 TrueOriginalPos;
        }
        private readonly Dictionary<RectTransform, ShakeState> activeShakes = new();

        private void Awake()
        {
            masterCts = new CancellationTokenSource();
        }

        private void OnDestroy()
        {
            CancelAll();
            masterCts?.Dispose();
        }

        /// <summary>
        /// Ghost overlay flies from source to target, then self-destructs.
        /// Used for stock draw, undo, auto-complete, tap-to-move.
        /// onComplete fires in finally — guaranteed on both normal completion and cancellation.
        /// </summary>
        public void AnimateMove(PlayingCard card, bool faceUp, Vector3 fromWorld, Vector3 toWorld,
            System.Action onComplete = null)
            => AnimateMove(card, faceUp, fromWorld, toWorld, moveDuration, onComplete);

        /// <summary>
        /// Same as <see cref="AnimateMove(PlayingCard,bool,Vector3,Vector3,System.Action)"/> but
        /// with an explicit per-call duration. Used by the win cascade where the default 0.2s
        /// would push 52 ghosts off-screen in 2 frames before the player can register them.
        /// </summary>
        public void AnimateMove(PlayingCard card, bool faceUp, Vector3 fromWorld, Vector3 toWorld,
            float duration, System.Action onComplete = null)
        {
            AnimateMoveInternalAsync(card, faceUp, fromWorld, toWorld, duration, onComplete, masterCts.Token).Forget();
        }

        /// <summary>
        /// Ghost hint preview: flies to target, then fades out.
        /// Real cards untouched. Call CancelAll() before next hint.
        /// </summary>
        public void AnimateHintPreview(PlayingCard card, bool faceUp,
            Vector3 sourceWorld, Vector3 targetWorld)
        {
            AnimateHintPreviewInternalAsync(card, faceUp, sourceWorld, targetWorld, masterCts.Token).Forget();
        }

        /// <summary>
        /// Immediately destroys all active ghost cards and restores any in-flight shake targets
        /// to their true original positions.
        /// </summary>
        public void CancelAll()
        {
            masterCts?.Cancel();
            masterCts?.Dispose();

            foreach (var ghost in activeGhosts)
            {
                if (ghost != null) Destroy(ghost);
            }
            activeGhosts.Clear();

            // Cancel before Dispose: disposing a CTS doesn't signal cancellation, so in-flight
            // shake tasks would keep writing positions until their next yield.
            foreach (var kvp in activeShakes)
            {
                kvp.Value.Cts?.Cancel();
                if (kvp.Key != null)
                    kvp.Key.anchoredPosition = kvp.Value.TrueOriginalPos;
                kvp.Value.Cts?.Dispose();
            }
            activeShakes.Clear();

            masterCts = new CancellationTokenSource();
        }

        // ─── Reject Shake ────────────────────────────────────────────

        /// <summary>
        /// Horizontally shakes a UI card to reject an invalid action. Re-tapping the same card
        /// while a shake is in flight pre-empts the old one so the origin doesn't drift.
        /// </summary>
        public void AnimateRejectShake(RectTransform target)
        {
            if (target == null) return;

            // Pre-empt any in-flight shake on this target so its captured origin isn't a mid-shake offset.
            if (activeShakes.TryGetValue(target, out var existing))
            {
                existing.Cts?.Cancel();
                target.anchoredPosition = existing.TrueOriginalPos;
                existing.Cts?.Dispose();
                activeShakes.Remove(target);
            }

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(masterCts.Token);
            var state = new ShakeState
            {
                Cts = linkedCts,
                TrueOriginalPos = target.anchoredPosition,
            };
            activeShakes[target] = state;

            AnimateRejectShakeInternalAsync(target, state, linkedCts.Token).Forget();
        }

        private async UniTaskVoid AnimateRejectShakeInternalAsync(
            RectTransform target, ShakeState state, CancellationToken ct)
        {
            try
            {
                float elapsed = 0f;
                while (elapsed < shakeDuration)
                {
                    ct.ThrowIfCancellationRequested();
                    // Break cleanly if target was destroyed mid-shake (scene unload) so we don't
                    // hit MissingReferenceException on anchoredPosition; the finally still runs.
                    if (target == null) break;
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / shakeDuration);

                    float decay = 1f - t;
                    float wave = Mathf.Sin(t * shakeOscillations * Mathf.PI * 2f);
                    float offsetX = wave * shakeIntensity * decay;

                    target.anchoredPosition = state.TrueOriginalPos + new Vector2(offsetX, 0f);
                    await UniTask.Yield(ct);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                // Clean up the dict entry even if target was destroyed; only position-restore needs a live target.
                if (activeShakes.TryGetValue(target, out var current)
                    && ReferenceEquals(current, state))
                {
                    if (target != null)
                        target.anchoredPosition = state.TrueOriginalPos;
                    state.Cts?.Dispose();
                    activeShakes.Remove(target);
                }
            }
        }

        // ─── Move Animation ──────────────────────────────────────────

        private async UniTaskVoid AnimateMoveInternalAsync(
            PlayingCard card, bool faceUp,
            Vector3 fromWorld, Vector3 toWorld,
            float duration,
            System.Action onComplete,
            CancellationToken ct)
        {
            var ghost = CreateGhost(card, faceUp, fromWorld);
            if (ghost == null)
            {
                onComplete?.Invoke();
                return;
            }

            try
            {
                var rt = ghost.GetComponent<RectTransform>();
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float curved = moveEase != null ? moveEase.Evaluate(t) : t;
                    rt.position = Vector3.Lerp(fromWorld, toWorld, curved);
                    await UniTask.Yield(ct);
                }
            }
            catch (System.OperationCanceledException) { }
            finally
            {
                DestroyGhost(ghost);
                onComplete?.Invoke();
            }
        }

        // ─── Hint Preview Animation ─────────────────────────────────

        private async UniTaskVoid AnimateHintPreviewInternalAsync(
            PlayingCard card, bool faceUp,
            Vector3 sourceWorld, Vector3 targetWorld,
            CancellationToken ct)
        {
            var ghost = CreateGhost(card, faceUp, sourceWorld);
            if (ghost == null) return;

            var cg = ghost.GetComponent<CanvasGroup>();
            if (cg == null) cg = ghost.AddComponent<CanvasGroup>();
            cg.alpha = previewGhostAlpha;

            try
            {
                var rt = ghost.GetComponent<RectTransform>();

                float elapsed = 0f;
                while (elapsed < previewOutDuration)
                {
                    ct.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / previewOutDuration);
                    float curved = moveEase != null ? moveEase.Evaluate(t) : t;
                    rt.position = Vector3.Lerp(sourceWorld, targetWorld, curved);
                    await UniTask.Yield(ct);
                }

                elapsed = 0f;
                float startAlpha = cg.alpha;
                while (elapsed < previewFadeDuration)
                {
                    ct.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / previewFadeDuration);
                    cg.alpha = Mathf.Lerp(startAlpha, 0f, t);
                    await UniTask.Yield(ct);
                }
            }
            catch (System.OperationCanceledException) { }
            finally
            {
                DestroyGhost(ghost);
            }
        }

        // ─── Ghost Lifecycle ─────────────────────────────────────────

        private GameObject CreateGhost(PlayingCard card, bool faceUp, Vector3 worldPos)
        {
            if (ghostPrefab == null || ghostRoot == null) return null;

            var ghost = Instantiate(ghostPrefab, ghostRoot);
            if (currentSpriteSet != null) ghost.SetSpriteSet(currentSpriteSet);
            ghost.SetCard(card);

            if (faceUp)
                ghost.OpenImmediate();
            else
                ghost.Close();

            ghost.Disable();
            var cg = ghost.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }

            ghost.rectTransform.position = worldPos;
            activeGhosts.Add(ghost.gameObject);
            return ghost.gameObject;
        }

        private void DestroyGhost(GameObject ghost)
        {
            if (ghost != null)
            {
                activeGhosts.Remove(ghost);
                Destroy(ghost);
            }
        }
    }
}
