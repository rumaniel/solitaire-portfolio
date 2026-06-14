using UnityEngine;
using UnityEngine.UI;

namespace Scene.Lobby.View
{
    /// <summary>
    /// Adjusts a GridLayoutGroup's column count based on screen width so the
    /// Lobby tile grid scales nicely on mobile (portrait), tablet, PC, and WebGL.
    ///
    /// Attach to the same GameObject as the GridLayoutGroup that hosts the tiles.
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public class LobbyResponsiveLayout : MonoBehaviour
    {
        [Tooltip("Screen width threshold (px) below which the grid collapses to a single column.")]
        [SerializeField] private int singleColumnBreakpoint = 600;

        [Tooltip("Number of columns when the screen is wider than the breakpoint.")]
        [SerializeField] private int wideColumnCount = 2;

        private GridLayoutGroup grid;
        private int lastWidth;
        private int lastHeight;

        private void Awake()
        {
            grid = GetComponent<GridLayoutGroup>();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            // Cheap polling: detect orientation changes / window resizes (PC, WebGL).
            if (Screen.width != lastWidth || Screen.height != lastHeight)
                Apply();
        }

        private void Apply()
        {
            if (grid == null) return;

            lastWidth = Screen.width;
            lastHeight = Screen.height;

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = lastWidth < singleColumnBreakpoint ? 1 : wideColumnCount;
        }
    }
}
