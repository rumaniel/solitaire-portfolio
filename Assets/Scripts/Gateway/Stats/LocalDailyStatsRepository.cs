using System;
using System.IO;
using Cysharp.Threading.Tasks;
using MemoryPack;
using Model.Stats;
using UnityEngine;

namespace Gateway.Stats
{
    /// <summary>
    /// Persists <see cref="DailyStats"/> as MemoryPack to
    /// <c>persistentDataPath/daily_stats.bin</c>. WebGL uses synchronous I/O per
    /// the snapshot pattern (no thread-pool).
    /// </summary>
    public class LocalDailyStatsRepository : IDailyStatsRepository
    {
        private static string GetPath()
            => Path.Combine(Application.persistentDataPath, "daily_stats.bin");

        public async UniTask<DailyStats> LoadAsync()
        {
            var path = GetPath();
            if (!File.Exists(path))
                return null;

            try
            {
                byte[] bytes;
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                    bytes = File.ReadAllBytes(path);
                else
                    bytes = await UniTask.RunOnThreadPool(() => File.ReadAllBytes(path));

                return MemoryPackSerializer.Deserialize<DailyStats>(bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DailyStats] Failed to load: {e.Message}");
                return null;
            }
        }

        public async UniTask SaveAsync(DailyStats stats)
        {
            // Persistence failures propagate — the data layer must not report success on a
            // failed write. Callers own the best-effort policy (see DailyStatsService).
            var path = GetPath();
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
