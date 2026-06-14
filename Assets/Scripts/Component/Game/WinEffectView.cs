using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Component.Game
{
    /// <summary>Thin wrapper around the win-celebration `ParticleSystem`. Played in parallel with
    /// `WinCascadeAnimator` from `IngameComponent.PlayWinCelebrationAsync`. No-op if the
    /// `confetti` SerializeField is unwired so unwired scenes still ship.</summary>
    public class WinEffectView : MonoBehaviour
    {
        [SerializeField] private ParticleSystem confetti;

        public async UniTask PlayAsync(CancellationToken ct = default)
        {
            if (confetti == null) return;
            ct.ThrowIfCancellationRequested();

            // `async` (not direct return) so the linked CTS lives across the await rather than
            // being disposed before WaitUntil even starts.
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                ct, this.GetCancellationTokenOnDestroy());
            var linkedCt = linkedCts.Token;

            confetti.Clear(true);
            confetti.Play(true);
            // Wait until the system has finished emitting AND every alive particle has expired.
            await UniTask.WaitUntil(() => confetti == null || !confetti.IsAlive(true),
                cancellationToken: linkedCt);
        }
    }
}
