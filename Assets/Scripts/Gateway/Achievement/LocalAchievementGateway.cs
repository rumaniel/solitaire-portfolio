using System;
using System.IO;
using Cysharp.Threading.Tasks;
using MemoryPack;
using Model.Achievement;
using UnityEngine;

namespace Gateway.Achievement
{
    /// <summary>
    /// Persists <see cref="AchievementStore"/> via MemoryPack to <c>persistentDataPath/achievements.bin</c>.
    /// I/O pattern mirrors <see cref="Gateway.Stats.LocalStatsRepository"/>.
    /// </summary>
    public class LocalAchievementGateway : IAchievementGateway
    {
        private static string GetPath()
            => Path.Combine(Application.persistentDataPath, "achievements.bin");

        public async UniTask<AchievementStore> LoadAsync()
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

                return MemoryPackSerializer.Deserialize<AchievementStore>(bytes);
            }
            catch (Exception e)
            {
                Debug.LogError("[Achievement] Failed to load store.");
                Debug.LogException(e);
                return null;
            }
        }

        public async UniTask SaveAsync(AchievementStore store)
        {
            // Persistence failures propagate — the caller (AchievementService.SaveAsync) owns the
            // single best-effort boundary; the data layer must not report a failed write as success.
            var path = GetPath();
            var bytes = MemoryPackSerializer.Serialize(store);

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
