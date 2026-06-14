using UnityEngine;
using VContainer;
using Core;
using Service.AudioService;

namespace Component.Audio
{
    public class AudioPlayer : ComponentBase
    {
        [SerializeField] private string key;

        [Inject] private IAudioService audioService;

        public void Play()
        {
            if (audioService == null || string.IsNullOrEmpty(key)) return;
            audioService.Play(key);
        }
    }
}
