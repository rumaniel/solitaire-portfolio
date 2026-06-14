using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Scene.Ingame.View
{
    /// <summary>Reusable FIFO toast. Show() enqueues; messages play sequentially with fade.</summary>
    public class ToastView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text messageText;

        [SerializeField]
        [Tooltip("Hold time (seconds) when Show() is called without an explicit duration.")]
        private float defaultDurationSeconds = 1.5f;

        [SerializeField]
        [Tooltip("Fade in / fade out duration (seconds). Set to 0 to disable fading.")]
        private float fadeDurationSeconds = 0.15f;

        private readonly Queue<(string message, float duration)> queue = new();
        private bool isPlaying;

        private void Awake()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>Enqueues a toast message and starts playback if idle.</summary>
        /// <param name="durationSeconds">Hold time; defaults to <see cref="defaultDurationSeconds"/>.</param>
        public void Show(string message, float? durationSeconds = null)
        {
            if (string.IsNullOrEmpty(message)) return;
            queue.Enqueue((message, durationSeconds ?? defaultDurationSeconds));
            if (!isPlaying) RunLoopAsync().Forget();
        }

        /// <summary>Clears the pending queue; the current toast finishes normally.</summary>
        public void Clear() => queue.Clear();

        /// <summary>Number of messages waiting to be displayed.</summary>
        public int QueuedCount => queue.Count;

        /// <summary>True while a message is being displayed.</summary>
        public bool IsPlaying => isPlaying;

        private async UniTaskVoid RunLoopAsync()
        {
            isPlaying = true;
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                while (queue.Count > 0)
                {
                    var (msg, duration) = queue.Dequeue();
                    if (messageText != null) messageText.SetText(msg);
                    if (panel != null) panel.SetActive(true);

                    await FadeAsync(0f, 1f, ct);
                    if (duration > 0f)
                    {
                        await UniTask.Delay(
                            System.TimeSpan.FromSeconds(duration),
                            ignoreTimeScale: true,
                            cancellationToken: ct);
                    }
                    await FadeAsync(1f, 0f, ct);

                    if (panel != null) panel.SetActive(false);
                }
            }
            catch (System.OperationCanceledException) { }
            finally
            {
                isPlaying = false;
            }
        }

        private async UniTask FadeAsync(float from, float to, CancellationToken ct)
        {
            if (canvasGroup == null)
                return;

            if (fadeDurationSeconds <= 0f)
            {
                canvasGroup.alpha = to;
                return;
            }

            float elapsed = 0f;
            while (elapsed < fadeDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDurationSeconds));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            canvasGroup.alpha = to;
        }
    }
}
