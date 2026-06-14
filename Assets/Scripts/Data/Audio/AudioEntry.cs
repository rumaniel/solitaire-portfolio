using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Data.Audio
{
    [Serializable]
    public class AudioEntry
    {
        [SerializeField] private AudioType audioType = AudioType.Sfx;

        [Header("Clip")]
        [SerializeField] private AudioClipReference clipReference;
        [SerializeField] private AudioClip[] extraClips;
        [SerializeField] private ClipSelectionMode clipSelectionMode = ClipSelectionMode.Single;

        [Header("Playback")]
        [SerializeField] private float delay;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private bool loop;

        [Header("Fade")]
        [SerializeField] private float fadeInDuration;
        [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float fadeOutDuration;
        [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Duplicate Policy")]
        [SerializeField] private DuplicatePlayPolicy duplicatePolicy = DuplicatePlayPolicy.All;
        [SerializeField] private int maxInstances = 1;

        [Header("Mixer")]
        [SerializeField] private AudioMixerGroup mixerGroup;

        public AudioType AudioType => audioType;
        public AudioClipReference ClipReference => clipReference;
        public AudioClip[] ExtraClips => extraClips;
        public ClipSelectionMode ClipSelectionMode => clipSelectionMode;
        public int ClipCount => 1 + (extraClips?.Length ?? 0);
        public float Delay => delay;
        public float Volume => volume;
        public bool Loop => loop;
        public float FadeInDuration => fadeInDuration;
        public AnimationCurve FadeInCurve => fadeInCurve;
        public float FadeOutDuration => fadeOutDuration;
        public AnimationCurve FadeOutCurve => fadeOutCurve;
        public DuplicatePlayPolicy DuplicatePolicy => duplicatePolicy;
        public int MaxInstances => maxInstances;
        public AudioMixerGroup MixerGroup => mixerGroup;

        public AudioClip GetClipAt(int index)
        {
            if (index <= 0) return clipReference.Clip;
            if (extraClips == null || index - 1 >= extraClips.Length) return clipReference.Clip;
            return extraClips[index - 1] ?? clipReference.Clip;
        }
    }
}
