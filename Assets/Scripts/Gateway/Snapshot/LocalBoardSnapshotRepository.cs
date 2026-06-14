using System;
using System.IO;
using Cysharp.Threading.Tasks;
using MemoryPack;
using Model.Board;
using UnityEngine;

namespace Gateway.Snapshot
{
    public class LocalBoardSnapshotRepository : IBoardSnapshotRepository
    {
        private static string GetPath(SnapshotKey key)
            => Path.Combine(Application.persistentDataPath, string.Concat("snapshot_", key.ToString(), ".bin"));

        public async UniTask<BoardSnapshot> LoadAsync(SnapshotKey key)
        {
            var path = GetPath(key);
            if (!File.Exists(path))
                return null;

            try
            {
                byte[] bytes;
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                    bytes = File.ReadAllBytes(path);
                else
                    bytes = await UniTask.RunOnThreadPool(() => File.ReadAllBytes(path));

                return MemoryPackSerializer.Deserialize<BoardSnapshot>(bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoardSnapshot] Failed to load snapshot for {key}: {e.Message}");
                return null;
            }
        }

        public async UniTask SaveAsync(SnapshotKey key, BoardSnapshot snapshot)
        {
            // Persistence failures propagate — the caller (BoardSnapshotService) owns the
            // best-effort boundary; the data layer must not report a failed write as success.
            var path = GetPath(key);
            var bytes = MemoryPackSerializer.Serialize(snapshot);

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

        public async UniTask DeleteAsync(SnapshotKey key)
        {
            var path = GetPath(key);
            if (!File.Exists(path))
                return;

            try
            {
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                    File.Delete(path);
                else
                    await UniTask.RunOnThreadPool(() => File.Delete(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoardSnapshot] Failed to delete snapshot for {key}: {e.Message}");
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
