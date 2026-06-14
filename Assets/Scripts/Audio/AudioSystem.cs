using System.Collections.Generic;
using System.Threading;
using Core;
using Cysharp.Threading.Tasks;
using Data.Audio;
using R3;
using Service.AudioService;
using UnityEngine;
using UnityEngine.Audio;
using VContainer;

namespace Audio
{
    public class AudioSystem : ComponentBase
    {
        [SerializeField] private AudioDatabaseAsset database;
        [SerializeField] private AudioMixer mixer;

        [Inject] private IAudioService AudioService { get; set; }

        private readonly List<AudioDatabaseAsset> databases = new();
        private readonly Stack<AudioSourcePlayer> pool = new();
        private readonly Dictionary<string, List<AudioSourcePlayer>> activePlayers = new();
        private readonly Dictionary<(string key, int channel), int> clipIndices = new();
        private int createdCount;

        // Music: single dedicated player
        private AudioSourcePlayer musicPlayer;
        private AudioEntry activeMusicEntry;
        private CancellationTokenSource musicCts;

        public void AddDatabase(AudioDatabaseAsset db)
        {
            if (db != null && !databases.Contains(db))
                databases.Add(db);
        }

        public void RemoveDatabase(AudioDatabaseAsset db) => databases.Remove(db);

        private void Start()
        {
            if (database != null && !databases.Contains(database))
                databases.Insert(0, database);

            InitMusicPlayers();

            if (AudioService == null) return;

            // SFX
            AudioService.OnPlay.Subscribe(a => Play(a.key, a.channel)).AddTo(this);
            AudioService.OnStopAll.Subscribe(_ => StopAll()).AddTo(this);
            AudioService.OnPause.Subscribe(_ => PauseAll()).AddTo(this);
            AudioService.OnUnPause.Subscribe(_ => UnPauseAll()).AddTo(this);

            // Music
            AudioService.OnPlayMusic.Subscribe(PlayMusic).AddTo(this);
            AudioService.OnStopMusic.Subscribe(_ => StopMusic()).AddTo(this);

            // Mixer
            AudioService.OnMusicMuteChanged.Subscribe(ApplyMusicMute).AddTo(this);
            AudioService.OnSfxMuteChanged.Subscribe(ApplySfxMute).AddTo(this);
            AudioService.OnSnapshotTransition
                .Subscribe(a => ApplySnapshotTransition(a.snapshotName, a.duration))
                .AddTo(this);

            ApplyMusicMute(AudioService.IsMusicMuted);
            ApplySfxMute(AudioService.IsSfxMuted);
        }

        // Unity only guarantees a PlayerPrefs flush on clean quit, so force one
        // when the app backgrounds on mobile to survive OS-initiated kills.
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) PlayerPrefs.Save();
        }

        private AudioEntry FindEntry(string key)
        {
            for (int i = 0; i < databases.Count; i++)
            {
                var entry = databases[i].Get(key);
                if (entry != null) return entry;
            }
            return null;
        }

        #region SFX Playback

        private void Play(string key, int channel = 0)
        {
            var entry = FindEntry(key);
            if (entry == null || !entry.ClipReference.HasClip) return;

            switch (entry.DuplicatePolicy)
            {
                case DuplicatePlayPolicy.All:
                    break;
                case DuplicatePlayPolicy.FirstN:
                    CleanupStalePlayers(key);
                    if (GetActiveCount(key) >= entry.MaxInstances) return;
                    break;
                case DuplicatePlayPolicy.LastN:
                    CleanupStalePlayers(key);
                    while (GetActiveCount(key) >= entry.MaxInstances)
                        StopOldest(key);
                    break;
            }

            var clip = SelectClip(entry, key, channel);
            var player = Rent();
            TrackPlayer(key, player);
            player.Play(entry, clip);

            var effectiveLoop = entry.AudioType != Data.Audio.AudioType.Sfx && entry.Loop;
            if (!effectiveLoop)
                AutoReleaseAsync(key, entry, player, destroyCancellationToken).Forget();
        }

        private AudioClip SelectClip(AudioEntry entry, string key, int channel)
        {
            if (entry.ClipSelectionMode == ClipSelectionMode.Single || entry.ClipCount <= 1)
                return entry.ClipReference.Clip;

            var indexKey = (key, channel);

            if (entry.ClipSelectionMode == ClipSelectionMode.Indexed)
                return entry.GetClipAt(channel % entry.ClipCount);

            if (entry.ClipSelectionMode == ClipSelectionMode.Random)
                return entry.GetClipAt(Random.Range(0, entry.ClipCount));

            // Sequential: advance per (key, channel)
            clipIndices.TryGetValue(indexKey, out int current);
            var clip = entry.GetClipAt(current);
            clipIndices[indexKey] = (current + 1) % entry.ClipCount;
            return clip;
        }

        private async UniTaskVoid AutoReleaseAsync(
            string key, AudioEntry entry, AudioSourcePlayer player, CancellationToken ct)
        {
            try
            {
                await player.WaitForEndAsync(entry, ct);

                // Guard: if StopAll() already returned this player to the pool, skip.
                if (!UntrackPlayer(key, player)) return;
                Return(player);
            }
            catch (System.OperationCanceledException) { }
        }

        private void StopAll()
        {
            // Stop SFX
            foreach (var kvp in activePlayers)
                foreach (var player in kvp.Value)
                {
                    player.Stop();
                    Return(player);
                }
            activePlayers.Clear();

            // Reset sequential playback state
            clipIndices.Clear();

            // Stop Music
            StopMusic();
        }

        private void PauseAll()
        {
            foreach (var kvp in activePlayers)
                foreach (var player in kvp.Value)
                    player.Pause();

            if (musicPlayer != null && musicPlayer.IsPlaying) musicPlayer.Pause();
        }

        private void UnPauseAll()
        {
            foreach (var kvp in activePlayers)
                foreach (var player in kvp.Value)
                    player.UnPause();

            if (musicPlayer != null && musicPlayer.IsPlaying) musicPlayer.UnPause();
        }

        #endregion

        #region Music

        private void InitMusicPlayers()
        {
            var go = new GameObject("MusicPlayer");
            go.transform.SetParent(transform, worldPositionStays: false);
            musicPlayer = go.AddComponent<AudioSourcePlayer>();
        }

        private void PlayMusic(string key)
        {
            var entry = FindEntry(key);
            if (entry == null || !entry.ClipReference.HasClip) return;

            CancelMusicTransition();
            musicCts = new CancellationTokenSource();

            var prevEntry = activeMusicEntry;
            activeMusicEntry = entry;

            TransitionMusicAsync(prevEntry, entry, musicCts.Token).Forget();
        }

        private void StopMusic()
        {
            if (musicPlayer == null || !musicPlayer.IsPlaying)
            {
                activeMusicEntry = null;
                return;
            }

            CancelMusicTransition();
            musicCts = new CancellationTokenSource();

            var entry = activeMusicEntry;
            activeMusicEntry = null;

            StopMusicAsync(entry, musicCts.Token).Forget();
        }

        private async UniTaskVoid TransitionMusicAsync(AudioEntry prevEntry, AudioEntry nextEntry, CancellationToken ct)
        {
            try
            {
                // Fade out current track if playing
                if (musicPlayer.IsPlaying && prevEntry != null)
                {
                    if (prevEntry.FadeOutDuration > 0f)
                        await musicPlayer.StopWithFadeAsync(prevEntry.FadeOutDuration, prevEntry.FadeOutCurve, ct);
                    else
                        musicPlayer.Stop();
                }
                else
                {
                    musicPlayer.Stop();
                }

                // Play new track
                musicPlayer.Play(nextEntry);
            }
            catch (System.OperationCanceledException) { }
        }

        private async UniTaskVoid StopMusicAsync(AudioEntry entry, CancellationToken ct)
        {
            try
            {
                if (entry != null && entry.FadeOutDuration > 0f)
                    await musicPlayer.StopWithFadeAsync(entry.FadeOutDuration, entry.FadeOutCurve, ct);
                else
                    musicPlayer.Stop();
            }
            catch (System.OperationCanceledException) { }
        }

        private void CancelMusicTransition()
        {
            musicCts?.Cancel();
            musicCts?.Dispose();
            musicCts = null;
        }

        #endregion

        #region Mixer

        private void ApplyMusicMute(bool muted)
        {
            if (mixer != null) mixer.SetFloat("MusicVolume", muted ? -80f : 0f);
        }

        private void ApplySfxMute(bool muted)
        {
            if (mixer != null) mixer.SetFloat("SfxVolume", muted ? -80f : 0f);
        }

        private void ApplySnapshotTransition(string snapshotName, float duration)
        {
            if (mixer == null) return;
            var snapshot = mixer.FindSnapshot(snapshotName);
            if (snapshot == null)
            {
                Debug.LogWarning($"AudioMixer snapshot not found: \"{snapshotName}\"");
                return;
            }
            snapshot.TransitionTo(duration);
        }

        #endregion

        #region Duplicate Policy

        private int GetActiveCount(string key)
        {
            return activePlayers.TryGetValue(key, out var list) ? list.Count : 0;
        }

        private void StopOldest(string key)
        {
            if (!activePlayers.TryGetValue(key, out var players) || players.Count == 0) return;
            var oldest = players[0];
            players.RemoveAt(0);
            oldest.Stop();
            Return(oldest);
        }

        private void CleanupStalePlayers(string key)
        {
            if (!activePlayers.TryGetValue(key, out var players)) return;
            players.RemoveAll(p => p == null || !p.IsPlaying);
        }

        private void TrackPlayer(string key, AudioSourcePlayer player)
        {
            if (!activePlayers.TryGetValue(key, out var players))
            {
                players = new List<AudioSourcePlayer>();
                activePlayers[key] = players;
            }
            players.Add(player);
        }

        private bool UntrackPlayer(string key, AudioSourcePlayer player)
        {
            if (activePlayers.TryGetValue(key, out var players))
                return players.Remove(player);
            return false;
        }

        #endregion

        #region Pool

        private AudioSourcePlayer Rent()
        {
            return pool.Count > 0 ? pool.Pop() : CreatePlayer();
        }

        private void Return(AudioSourcePlayer player)
        {
            player.ResetState();
            pool.Push(player);
        }

        private AudioSourcePlayer CreatePlayer()
        {
            var child = new GameObject(string.Concat("AudioSource_", createdCount++.ToString()));
            child.transform.SetParent(transform, worldPositionStays: false);
            return child.AddComponent<AudioSourcePlayer>();
        }

        #endregion
    }
}
