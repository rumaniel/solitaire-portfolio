namespace Model.Game
{
    public static class GameTypeExtensions
    {
        /// <summary>True for layered-board games (Pyramid, TriPeaks). Decides which presenter owns the route in the merged Ingame scene.</summary>
        public static bool IsBoardMode(this GameType gameType)
            => gameType == GameType.Pyramid || gameType == GameType.TriPeaks;
    }
}
