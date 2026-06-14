using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;

namespace Service.RouteService
{
    public class RouteService : IRouteService, IDisposable
    {
        private readonly List<(string path, Dictionary<string, string> query)> history = new();
        private const int MaxHistory = 50; // max number of entries to keep

        private string currentPath;
        private Dictionary<string, string> currentQuery = new();

        private readonly Subject<Unit> samePathNavigatedSubject = new();
        private readonly ReactiveProperty<bool> isNavigating = new(false);
        private readonly ReactiveProperty<bool> isBlocking = new(false);

        public string CurrentPath => currentPath;
        public IReadOnlyDictionary<string, string> CurrentQuery => currentQuery;
        public Observable<Unit> OnSamePathNavigated => samePathNavigatedSubject;
        public ReadOnlyReactiveProperty<bool> IsNavigating => isNavigating;
        public ReadOnlyReactiveProperty<bool> IsBlocking => isBlocking;

        private System.Func<string, UniTask> sceneLoader;

        public void Initialize(System.Func<string, UniTask> sceneLoader)
        {
            this.sceneLoader = sceneLoader ?? throw new System.ArgumentNullException(nameof(sceneLoader));
        }

        public async UniTask NavigateAsync(string path, Dictionary<string, string> query = null, bool useBlocker = true)
        {
            // Reject duplicate clicks while a scene load is in flight.
            if (isNavigating.Value)
            {
                UnityEngine.Debug.LogWarning(
                    $"[RouteService] Navigation to '{path}' ignored — another navigation is in progress.");
                return;
            }

            if (currentPath == path)
            {
                currentQuery = query ?? new Dictionary<string, string>();
                samePathNavigatedSubject.OnNext(Unit.Default);
                return;
            }

            if (sceneLoader == null)
                throw new System.InvalidOperationException("RouteService not initialized with a scene loader.");

            isNavigating.Value = true;
            isBlocking.Value = useBlocker;
            try
            {
                if (!string.IsNullOrEmpty(currentPath))
                {
                    history.Add((currentPath, new Dictionary<string, string>(currentQuery)));
                    if (history.Count > MaxHistory)
                    {
                        history.RemoveAt(0);
                    }
                }

                currentPath = path;
                currentQuery = query ?? new Dictionary<string, string>();
                await sceneLoader(path);
            }
            finally
            {
                // App teardown can dispose this singleton while a navigation is mid-await; guard the
                // reset so the finally doesn't throw on an already-disposed ReactiveProperty
                // (same teardown-race convention as the stats/achievement services).
                try
                {
                    isNavigating.Value = false;
                    isBlocking.Value = false;
                }
                catch (ObjectDisposedException) { }
            }
        }

        public async UniTask GoBackAsync(bool useBlocker = true)
        {
            if (history.Count == 0) return;

            if (isNavigating.Value)
            {
                UnityEngine.Debug.LogWarning(
                    "[RouteService] GoBackAsync ignored — another navigation is in progress.");
                return;
            }

            if (sceneLoader == null)
                throw new System.InvalidOperationException("RouteService not initialized with a scene loader.");

            isNavigating.Value = true;
            isBlocking.Value = useBlocker;
            try
            {
                var lastIndex = history.Count - 1;
                var (prevPath, prevQuery) = history[lastIndex];
                history.RemoveAt(lastIndex);
                currentPath = prevPath;
                currentQuery = prevQuery;
                await sceneLoader(prevPath);
            }
            finally
            {
                // App teardown can dispose this singleton while a navigation is mid-await; guard the
                // reset so the finally doesn't throw on an already-disposed ReactiveProperty
                // (same teardown-race convention as the stats/achievement services).
                try
                {
                    isNavigating.Value = false;
                    isBlocking.Value = false;
                }
                catch (ObjectDisposedException) { }
            }
        }

        public void Dispose()
        {
            samePathNavigatedSubject.Dispose();
            isNavigating.Dispose();
            isBlocking.Dispose();
        }
    }
}
