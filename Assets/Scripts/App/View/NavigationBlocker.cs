using R3;
using Service.RouteService;
using UnityEngine;
using VContainer;

namespace App.View
{
    /// <summary>Full-screen raycast blocker that activates during scene transitions via IRouteService.IsNavigating.</summary>
    public class NavigationBlocker : MonoBehaviour
    {
        [Tooltip("Root CanvasGroup whose blocksRaycasts flag is toggled with IsNavigating.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject objectToToggle;

        [Inject] private IRouteService RouteService { get; set; }

        private readonly CompositeDisposable disposable = new();

        private void Start()
        {
            if (RouteService == null)
            {
                Debug.LogWarning("[NavigationBlocker] IRouteService not injected — blocker will never activate.");
                return;
            }

            RouteService.IsBlocking
                .Subscribe(ApplyBlocking)
                .AddTo(disposable);
        }

        private void ApplyBlocking(bool navigating)
        {
            if (canvasGroup == null) return;
            canvasGroup.blocksRaycasts = navigating;
            objectToToggle.SetActive(navigating);
        }

        private void OnDestroy() => disposable.Dispose();
    }
}
