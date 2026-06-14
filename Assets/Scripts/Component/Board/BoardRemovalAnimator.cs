using System;
using System.Collections.Generic;
using System.Threading;
using Component.Card;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Component.Board
{
    /// <summary>
    /// Spins a matched board card and flies it straight down off the bottom edge, then destroys it.
    /// Animates the real removed card (no ghost) — fire-and-forget; board state is already updated
    /// before this plays. Mirrors CardMoveAnimator's manual UniTask-lerp style.
    /// </summary>
    public class BoardRemovalAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform overlayRoot; // flying cards reparent here (renders above cells)

        [Header("Flight")]
        [SerializeField, Min(0.05f)] private float duration = 0.6f;
        [SerializeField, Min(0.05f)] private float playToWasteDuration = 0.24f;
        [SerializeField] private float spinDegrees = 540f;               // total |Z| spin; direction randomized per card
        [SerializeField, Min(0f)] private float horizontalDrift = 120f;  // random horizontal drift while falling
        [SerializeField, Min(0f)] private float extraFallPadding = 200f; // ensures the card clears the bottom edge
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Fade")]
        [SerializeField] private bool fadeOut = true;
        [SerializeField, Range(0f, 1f)] private float fadeStartT = 0.7f;  // begin fading after this fraction of the flight

        private CancellationTokenSource masterCts;
        private readonly List<GameObject> active = new();

        private void Awake() => masterCts = new CancellationTokenSource();

        private void OnDestroy()
        {
            masterCts?.Cancel();
            masterCts?.Dispose();
            foreach (var go in active)
                if (go != null) Destroy(go);
            active.Clear();
        }

        /// <summary>Reparents the card to the overlay, spins + drops it off-screen, then destroys it.</summary>
        public void AnimateRemoval(UICard card)
        {
            if (card == null) return;
            if (overlayRoot == null) { Destroy(card.gameObject); return; }
            AnimateAsync(card, masterCts.Token).Forget();
        }

        /// <summary>Slides a played card from its cell to the waste anchor (world-space lerp), scaling to the
        /// waste card's scale, then invokes <paramref name="onArrived"/> so the controller can adopt it as the
        /// waste card. Unlike AnimateRemoval, the card is NOT destroyed.</summary>
        public void FlyToWaste(UICard card, RectTransform wasteAnchor, float targetScale, Action onArrived)
        {
            if (card == null) { onArrived?.Invoke(); return; }
            if (overlayRoot == null || wasteAnchor == null) { onArrived?.Invoke(); return; }
            FlyToWasteAsync(card, wasteAnchor, targetScale, onArrived, masterCts.Token).Forget();
        }

        private async UniTaskVoid FlyToWasteAsync(UICard card, RectTransform wasteAnchor, float targetScale,
            Action onArrived, CancellationToken ct)
        {
            var rt = card.rectTransform;
            rt.SetParent(overlayRoot, true); // keep world position
            rt.SetAsLastSibling();           // render above remaining cells while it flies
            card.Disable();                  // not tappable mid-flight
            active.Add(card.gameObject);

            Vector3 startPos = rt.position;
            Vector3 startScale = rt.localScale;
            Vector3 endScale = new Vector3(targetScale, targetScale, 1f);
            try
            {
                float elapsed = 0f;
                while (elapsed < playToWasteDuration)
                {
                    ct.ThrowIfCancellationRequested();
                    if (rt == null) break;
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / playToWasteDuration);
                    float curved = ease != null ? ease.Evaluate(t) : t;
                    rt.position = Vector3.Lerp(startPos, wasteAnchor.position, curved);
                    rt.localScale = Vector3.Lerp(startScale, endScale, curved);
                    await UniTask.Yield(ct);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                active.Remove(card.gameObject);
                if (card != null) onArrived?.Invoke(); // controller re-parents + adopts as waste card
            }
        }

        private async UniTaskVoid AnimateAsync(UICard card, CancellationToken ct)
        {
            var rt = card.rectTransform;
            rt.SetParent(overlayRoot, true); // keep world position
            rt.SetAsLastSibling();           // render above remaining cells
            card.Disable();                  // a flying card cannot be tapped
            active.Add(card.gameObject);

            Vector2 startPos = rt.anchoredPosition;
            float cardHeight = rt.rect.height;
            float bottomY = -(overlayRoot.rect.height * 0.5f) - cardHeight - extraFallPadding;
            Vector2 endPos = new Vector2(startPos.x + UnityEngine.Random.Range(-horizontalDrift, horizontalDrift), bottomY);
            float startZ = rt.localEulerAngles.z;
            float spin = spinDegrees * (UnityEngine.Random.value < 0.5f ? 1f : -1f);

            try
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();
                    if (rt == null) break;
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float curved = ease != null ? ease.Evaluate(t) : t;
                    rt.anchoredPosition = Vector2.Lerp(startPos, endPos, curved);
                    rt.localRotation = Quaternion.Euler(0f, 0f, startZ + spin * t);
                    if (fadeOut && t > fadeStartT)
                        card.SetAlpha(Mathf.InverseLerp(1f, fadeStartT, t)); // 1 -> 0 across [fadeStartT, 1]
                    await UniTask.Yield(ct);
                }
            }
            catch (System.OperationCanceledException) { }
            finally
            {
                active.Remove(card.gameObject);
                // OnDestroy's sweep may have already destroyed this; Unity's Destroy is idempotent
                // and card != null returns false once destroyed, so this never double-destroys.
                if (card != null) Destroy(card.gameObject);
            }
        }
    }
}
