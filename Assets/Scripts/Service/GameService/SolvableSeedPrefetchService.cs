using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Model.Board;
using Model.Game;
using UnityEngine;

namespace Service.GameService
{
    public class SolvableSeedPrefetchService : ISolvableSeedPrefetchService
    {
        private readonly Dictionary<(GameType, int), int> ready = new();
        private readonly HashSet<(GameType, int)> pending = new();
        private readonly object gate = new();

        public int? TryConsume(GameType gameType, int variant)
        {
            lock (gate)
            {
                var key = (gameType, variant);
                if (!ready.TryGetValue(key, out int seed)) return null;
                ready.Remove(key);
                return seed;
            }
        }

        public void Prefetch(GameType gameType, int variant, IDealRule rule)
        {
            if (rule == null) throw new System.ArgumentNullException(nameof(rule));
            if (!TryBeginPending(gameType, variant)) return;
            var key = (gameType, variant);

            // Route through SolverScheduler: thread-pool on native, PlayerLoop slices on WebGL.
            UniTask.Void(async () =>
            {
                try
                {
                    int inputSeed = DeckFactory.CreateRandomSeed();
                    var resolved = await SolverScheduler.ResolveAsync(inputSeed, rule);
                    lock (gate)
                    {
                        pending.Remove(key);
                        ready[key] = resolved.Seed;
                    }
                }
                catch (System.Exception e)
                {
                    // Remove from pending so a future Prefetch call can retry.
                    lock (gate) { pending.Remove(key); }
                    Debug.LogException(e);
                }
            });
        }

        public void PrefetchBoard(GameType gameType, int variant, BoardLayout layout)
        {
            if (layout == null) throw new System.ArgumentNullException(nameof(layout));
            if (!TryBeginPending(gameType, variant)) return;
            var key = (gameType, variant);

            UniTask.Void(async () =>
            {
                try
                {
                    int inputSeed = DeckFactory.CreateRandomSeed();
                    var resolved = await SolverScheduler.ResolveBoardAsync(inputSeed, gameType, layout);
                    lock (gate)
                    {
                        pending.Remove(key);
                        ready[key] = resolved.Seed;
                    }
                }
                catch (System.Exception e)
                {
                    lock (gate) { pending.Remove(key); }
                    Debug.LogException(e);
                }
            });
        }

        // Returns true and marks the key pending if neither ready nor already pending; false otherwise.
        private bool TryBeginPending(GameType gameType, int variant)
        {
            var key = (gameType, variant);
            lock (gate)
            {
                if (ready.ContainsKey(key) || pending.Contains(key)) return false;
                pending.Add(key);
                return true;
            }
        }
    }
}
