using System.Collections.Generic;
using Component.Board;
using Model.Game;

namespace Scene.Board
{
    /// <summary>
    /// The board view (<see cref="UIBoardController"/>) for each board game in the scene. Both board
    /// prefab instances live in the Ingame scene; the presenter activates the one matching the route's game
    /// type. A null entry (a board not yet present) is tolerated and skipped by <see cref="All"/>.
    /// </summary>
    public sealed class BoardViewSet
    {
        private readonly UIBoardController pyramid;
        private readonly UIBoardController triPeaks;

        public BoardViewSet(UIBoardController pyramid, UIBoardController triPeaks)
        {
            this.pyramid = pyramid;
            this.triPeaks = triPeaks;
        }

        /// <summary>The board view for a game type (TriPeaks → its board, everything else → the Pyramid board).</summary>
        public UIBoardController For(GameType gameType)
            => gameType == GameType.TriPeaks ? triPeaks : pyramid;

        /// <summary>All present board views (nulls skipped) — used to wire input and toggle visibility.</summary>
        public IEnumerable<UIBoardController> All
        {
            get
            {
                if (pyramid != null) yield return pyramid;
                if (triPeaks != null) yield return triPeaks;
            }
        }
    }
}
