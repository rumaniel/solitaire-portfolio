using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Component.Game
{
    /// <summary>
    /// Looping "suit hop" animation for the deal-loading overlay. Each direct child
    /// (the suit glyphs) hops in a staggered traveling wave so the overlay reads as
    /// active rather than frozen. Animates while enabled and restores the authored
    /// layout on disable. Attach to the overlay root; child order defines hop order.
    /// </summary>
    public class DealLoadingAnimator : MonoBehaviour
    {
        [Header("Hop")]
        [SerializeField, Min(0f)] private float hopHeight = 28f;
        [SerializeField, Min(0.1f)] private float cycleDuration = 0.9f;
        // Fraction of each child's cycle spent airborne. Small = crisp one-at-a-time hops
        // with a grounded gap between them; 1 = a continuous wave with no child ever resting.
        [SerializeField, Range(0.1f, 1f)] private float hopWindow = 0.55f;
        // Extra scale at the apex of a hop (0 = none). A small pop adds liveliness.
        [SerializeField, Range(0f, 0.5f)] private float apexScalePop = 0.12f;

        private RectTransform[] glyphs;
        private Vector2[] basePositions;
        private CancellationTokenSource cts;

        private void Awake()
        {
            int count = transform.childCount;
            glyphs = new RectTransform[count];
            basePositions = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                glyphs[i] = (RectTransform)transform.GetChild(i);
                basePositions[i] = glyphs[i].anchoredPosition;
            }
        }

        private void OnEnable()
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            AnimateAsync(cts.Token).Forget();
        }

        private void OnDisable()
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
            RestoreBase();
        }

        private async UniTaskVoid AnimateAsync(CancellationToken ct)
        {
            if (glyphs == null || glyphs.Length == 0) return;

            float elapsed = 0f;
            float stagger = 1f / glyphs.Length;

            try
            {
                while (true)
                {
                    // Unscaled so the loader keeps moving even if Time.timeScale is 0.
                    elapsed += Time.unscaledDeltaTime;
                    float loopT = Mathf.Repeat(elapsed / cycleDuration, 1f);

                    for (int i = 0; i < glyphs.Length; i++)
                    {
                        var rt = glyphs[i];
                        if (rt == null) continue;
                        float hop = Hop(Mathf.Repeat(loopT - (i * stagger), 1f));
                        rt.anchoredPosition = basePositions[i] + new Vector2(0f, hopHeight * hop);
                        rt.localScale = Vector3.one * (1f + (apexScalePop * hop));
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException) { /* stopped on disable/destroy */ }
        }

        /// <summary>0→1→0 airborne arc occupying only the first <see cref="hopWindow"/> of the cycle.</summary>
        private float Hop(float phase)
        {
            if (phase >= hopWindow) return 0f;
            return Mathf.Sin((phase / hopWindow) * Mathf.PI);
        }

        private void RestoreBase()
        {
            if (glyphs == null) return;
            for (int i = 0; i < glyphs.Length; i++)
            {
                if (glyphs[i] == null) continue;
                glyphs[i].anchoredPosition = basePositions[i];
                glyphs[i].localScale = Vector3.one;
            }
        }
    }
}
