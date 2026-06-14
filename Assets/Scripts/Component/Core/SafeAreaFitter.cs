using NaughtyAttributes;
using UnityEngine;

namespace Component.Core
{
    /// <summary>
    /// Fits a RectTransform's anchors to Screen.safeArea.
    /// Attach to a stretched RectTransform whose parent covers the full screen;
    /// a non-fullscreen parent would produce incorrect anchor normalization.
    /// Use the inspector "Apply Safe Area" button to preview at edit time.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private bool applyTop = true;
        [SerializeField] private bool applyBottom = true;
        [SerializeField] private bool applyLeft = true;
        [SerializeField] private bool applyRight = true;

        private RectTransform rect;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void Awake() => rect = GetComponent<RectTransform>();
        private void OnEnable() => Apply();

        private void Update()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != lastSafeArea || size != lastScreenSize)
                Apply();
        }

        [Button("Apply Safe Area")]
        private void Apply()
        {
            if (rect == null) rect = GetComponent<RectTransform>();
            if (Screen.width == 0 || Screen.height == 0) return;

            var safe = Screen.safeArea;
            lastSafeArea = safe;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            float xMin = applyLeft ? safe.xMin : 0f;
            float xMax = applyRight ? safe.xMax : Screen.width;
            float yMin = applyBottom ? safe.yMin : 0f;
            float yMax = applyTop ? safe.yMax : Screen.height;

            rect.anchorMin = new Vector2(xMin / Screen.width, yMin / Screen.height);
            rect.anchorMax = new Vector2(xMax / Screen.width, yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
