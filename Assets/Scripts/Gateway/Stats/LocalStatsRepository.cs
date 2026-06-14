using System;
using System.IO;
using Cysharp.Threading.Tasks;
using MemoryPack;
using Model.Game;
using Model.Stats;
using UnityEngine;

namespace Gateway.Stats
{
    /// <summary>
    /// Persists <see cref="LifetimeStats"/> via MemoryPack to
    /// <c>persistentDataPath/stats_{GameType}.bin</c>. Pre-release — no JSON
    /// compatibility path; the I/O pattern mirrors
    /// <see cref="Gateway.Snapshot.LocalGameSnapshotRepository"/>.
    /// </summary>
    public class LocalStatsRepository : IStatsRepository
    {
        private static string GetPath(GameType gameType)
            => Path.Combine(Application.persistentDataPath, string.Concat("stats_", gameType.ToString(), ".bin"));

        public async UniTask<LifetimeStats> LoadAsync(GameType gameType)
        {
            var path = GetPath(gameType);
            if (!File.Exists(path))
                return null;

            try
            {
                byte[] bytes;
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                    bytes = File.ReadAllBytes(path);
                else
                    bytes = await UniTask.RunOnThreadPool(() => File.ReadAllBytes(path));

                return MemoryPackSerializer.Deserialize<LifetimeStats>(bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Stats] Failed to load stats for {gameType}: {e.Message}");
                return null;
            }
        }

        public async UniTask SaveAsync(GameType gameType, LifetimeStats stats)
        {
            // Persistence failures propagate — the data layer must not report success on a
            // failed write. Callers own the best-effort policy (see LifetimeStatsService).
            var path = GetPath(gameType);
            var bytes = MemoryPackSerializer.Serialize(stats);

            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                EnsureDirectory(path);
                File.WriteAllBytes(path, bytes);
            }
            else
            {
                await UniTask.RunOnThreadPool(() =>
                {
                    EnsureDirectory(path);
                    File.WriteAllBytes(path, bytes);
                });
            }
        }

        private static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
