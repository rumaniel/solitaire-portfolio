using System;
using R3;
using UnityEngine;

namespace Service.AudioService
{
    public class AudioService : IAudioService, IDisposable
    {
        private const string MusicMutedKey = "audio.music.muted";
        private const string SfxMutedKey = "audio.sfx.muted";

        private readonly Subject<(string key, int channel)> onPlay = new();
        private readonly Subject<Unit> onStopAll = new();
        private readonly Subject<Unit> onPause = new();
        private readonly Subject<Unit> onUnPause = new();
        private readonly Subject<bool> onMusicMuteChanged = new();
        private readonly Subject<bool> onSfxMuteChanged = new();
        private readonly Subject<(string snapshotName, float duration)> onSnapshotTransition = new();
        private readonly Subject<string> onPlayMusic = new();
        private readonly Subject<Unit> onStopMusic = new();

        private bool musicMuted;
        private bool sfxMuted;

        public AudioService()
        {
            musicMuted = PlayerPrefs.GetInt(MusicMutedKey, 0) == 1;
            sfxMuted = PlayerPrefs.GetInt(SfxMutedKey, 0) == 1;
        }

        public Observable<(string key, int channel)> OnPlay => onPlay;
        public Observable<Unit> OnStopAll => onStopAll;
        public Observable<Unit> OnPause => onPause;
        public Observable<Unit> OnUnPause => onUnPause;
        public Observable<bool> OnMusicMuteChanged => onMusicMuteChanged;
        public Observable<bool> OnSfxMuteChanged => onSfxMuteChanged;
        public Observable<(string snapshotName, float duration)> OnSnapshotTransition => onSnapshotTransition;
        public Observable<string> OnPlayMusic => onPlayMusic;
        public Observable<Unit> OnStopMusic => onStopMusic;

        public bool IsMusicMuted => musicMuted;
        public bool IsSfxMuted => sfxMuted;

        public void Play(string key, int channel = 0)
        {
            if (!string.IsNullOrEmpty(key))
                onPlay.OnNext((key, channel));
        }

        public void StopAll() => onStopAll.OnNext(Unit.Default);
        public void Pause() => onPause.OnNext(Unit.Default);
        public void UnPause() => onUnPause.OnNext(Unit.Default);

        public void PlayMusic(string key)
        {
            if (!string.IsNullOrEmpty(key))
                onPlayMusic.OnNext(key);
        }

        public void StopMusic() => onStopMusic.OnNext(Unit.Default);

        public void SetMusicMuted(bool muted)
        {
            musicMuted = muted;
            PlayerPrefs.SetInt(MusicMutedKey, muted ? 1 : 0);
            onMusicMuteChanged.OnNext(muted);
        }

        public void SetSfxMuted(bool muted)
        {
            sfxMuted = muted;
            PlayerPrefs.SetInt(SfxMutedKey, muted ? 1 : 0);
            onSfxMuteChanged.OnNext(muted);
        }

        public void TransitionToSnapshot(string snapshotName, float duration)
            => onSnapshotTransition.OnNext((snapshotName, duration));

        public void Dispose()
        {
            onPlay.Dispose();
            onStopAll.Dispose();
            onPause.Dispose();
            onUnPause.Dispose();
            onMusicMuteChanged.Dispose();
            onSfxMuteChanged.Dispose();
            onSnapshotTransition.Dispose();
            onPlayMusic.Dispose();
            onStopMusic.Dispose();
        }
    }
}
