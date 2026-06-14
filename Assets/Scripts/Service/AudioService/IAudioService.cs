using R3;

namespace Service.AudioService
{
    public interface IAudioService
    {
        Observable<(string key, int channel)> OnPlay { get; }
        Observable<Unit> OnStopAll { get; }
        Observable<Unit> OnPause { get; }
        Observable<Unit> OnUnPause { get; }
        Observable<bool> OnMusicMuteChanged { get; }
        Observable<bool> OnSfxMuteChanged { get; }
        Observable<(string snapshotName, float duration)> OnSnapshotTransition { get; }

        Observable<string> OnPlayMusic { get; }
        Observable<Unit> OnStopMusic { get; }

        void Play(string key, int channel = 0);
        void StopAll();
        void Pause();
        void UnPause();

        void PlayMusic(string key);
        void StopMusic();

        void SetMusicMuted(bool muted);
        void SetSfxMuted(bool muted);
        bool IsMusicMuted { get; }
        bool IsSfxMuted { get; }

        void TransitionToSnapshot(string snapshotName, float duration);
    }
}
