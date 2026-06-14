using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;

namespace Service.RouteService
{
    public interface IRouteService
    {
        /// <summary>True while a scene load is in progress.</summary>
        ReadOnlyReactiveProperty<bool> IsNavigating { get; }

        /// <summary>True while navigating with <c>useBlocker = true</c>. NavigationBlocker subscribes to this.</summary>
        ReadOnlyReactiveProperty<bool> IsBlocking { get; }

        /// <summary>Navigate to a scene by name with optional query parameters.</summary>
        UniTask NavigateAsync(string path, Dictionary<string, string> query = null, bool useBlocker = true);

        /// <summary>Go back to the previous route in the navigation stack.</summary>
        UniTask GoBackAsync(bool useBlocker = true);

        /// <summary>Initialize with a platform-specific scene loader. Must be called before NavigateAsync.</summary>
        void Initialize(System.Func<string, UniTask> sceneLoader);

        /// <summary>Current route path (scene name).</summary>
        string CurrentPath { get; }

        /// <summary>Current query parameters.</summary>
        IReadOnlyDictionary<string, string> CurrentQuery { get; }

        /// <summary>Emitted when NavigateAsync targets the current path; query is updated without reloading.</summary>
        Observable<Unit> OnSamePathNavigated { get; }
    }
}
