using NaughtyAttributes;
using UnityEngine;

namespace Component.Core
{
    /// <summary>
    /// Extends a RectTransform outward into Screen.safeArea on enabled edges.
    /// Peer of <see cref="SafeAreaFitter"/>: Fitter shrinks, Padding extends.
    /// Each padded edge's anchor must already sit on the safe-area boundary
    /// (typically via a SafeAreaFitter on an ancestor). Owns offsetMin/offsetMax;
    /// do not attach to a rect driven by a LayoutGroup or animation.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaPadding : MonoBehaviour
    {
        [SerializeField] private bool padTop;
        [SerializeField] private bool padBottom = true;
        [SerializeField] private bool padLeft;
        [SerializeField] private bool padRight;

        private RectTransform rect;
        private Canvas canvas;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private float lastScaleFactor;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
        }

        private void OnEnable() => Apply();

        private void Update()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            var scale = canvas != null ? canvas.scaleFactor : 1f;
            if (Screen.safeArea != lastSafeArea || size != lastScreenSize || !Mathf.Approximately(scale, lastScaleFactor))
                Apply();
        }

        [Button("Apply Safe Area Padding")]
        private void Apply()
        {
            if (rect == null) rect = GetComponent<RectTransform>();
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (Screen.width == 0 || Screen.height == 0) return;

            var safe = Screen.safeArea;
            lastSafeArea = safe;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            lastScaleFactor = scale;

            var oMin = rect.offsetMin;
            var oMax = rect.offsetMax;
            if (padLeft) oMin.x = -safe.xMin / scale;
            if (padBottom) oMin.y = -safe.yMin / scale;
            if (padRight) oMax.x = (Screen.width - safe.xMax) / scale;
            if (padTop) oMax.y = (Screen.height - safe.yMax) / scale;
            rect.offsetMin = oMin;
            rect.offsetMax = oMax;
        }
    }
}
