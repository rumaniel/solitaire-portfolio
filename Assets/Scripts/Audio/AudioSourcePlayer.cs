using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data.Audio;
using UnityEngine;
using AudioType = Data.Audio.AudioType;

namespace Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioSourcePlayer : MonoBehaviour
    {
        private AudioSource source;
        private CancellationTokenSource fadeCts;

        public AudioSource Source => source;
        public bool IsPlaying => source != null && source.isPlaying;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
        }

        public void Play(AudioEntry entry) => Play(entry, entry.ClipReference.Clip);

        public void Play(AudioEntry entry, AudioClip clip)
        {
            source.clip = clip;
            source.volume = entry.FadeInDuration > 0f ? 0f : entry.Volume;
            source.loop = entry.AudioType != AudioType.Sfx && entry.Loop;
            source.outputAudioMixerGroup = entry.MixerGroup;

            source.PlayDelayed(entry.Delay);

            if (entry.FadeInDuration > 0f)
            {
                CancelFade();
                fadeCts = new CancellationTokenSource();
                FadeAsync(0f, entry.Volume, entry.FadeInDuration, entry.FadeInCurve, fadeCts.Token).Forget();
            }
        }

        public async UniTask WaitForEndAsync(AudioEntry entry, CancellationToken ct)
        {
            var totalDuration = source.clip.length + entry.Delay;

            if (entry.FadeOutDuration > 0f)
            {
                float waitTime = totalDuration - entry.FadeOutDuration;
                if (waitTime > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: ct);

                CancelFade();
                fadeCts = new CancellationTokenSource();
                var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, fadeCts.Token);
                await FadeAsync(source.volume, 0f, entry.FadeOutDuration, entry.FadeOutCurve, linked.Token);
            }
            else
            {
                await UniTask.Delay(TimeSpan.FromSeconds(totalDuration), cancellationToken: ct);
            }
        }

        public async UniTask StopWithFadeAsync(float duration, AnimationCurve curve, CancellationToken ct)
        {
            if (duration > 0f && source.isPlaying)
            {
                CancelFade();
                fadeCts = new CancellationTokenSource();
                var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, fadeCts.Token);
                await FadeAsync(source.volume, 0f, duration, curve, linked.Token);
            }
            Stop();
        }

        public void Stop()
        {
            CancelFade();
            source.Stop();
        }

        public void Pause() => source.Pause();

        public void UnPause() => source.UnPause();

        public void ResetState()
        {
            CancelFade();
            source.Stop();
            source.clip = null;
            source.outputAudioMixerGroup = null;
            source.loop = false;
            source.volume = 1f;
        }

        private async UniTask FadeAsync(float from, float to, float duration, AnimationCurve curve, CancellationToken ct)
        {
            float elapsed = 0f;
            source.volume = from;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveValue = curve != null ? curve.Evaluate(t) : t;
                source.volume = Mathf.Lerp(from, to, curveValue);
                await UniTask.Yield(ct);
            }
            source.volume = to;
        }

        private void CancelFade()
        {
            fadeCts?.Cancel();
            fadeCts?.Dispose();
            fadeCts = null;
        }
    }
}
