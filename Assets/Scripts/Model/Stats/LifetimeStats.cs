using MemoryPack;

namespace Model.Stats
{
    [MemoryPackable]
    public partial class LifetimeStats
    {
        public int TotalGamesPlayed;
        public int TotalGamesWon;
        public int TotalGamesLost;

        public float ShortestWinTime = float.MaxValue;
        public float LongestWinTime;
        public float TotalWinTime;

        public int MinWinMoves = int.MaxValue;
        public int MaxWinMoves;
        public int TotalWinMoves;

        public int GamesWonWithoutUndo;
        public int GamesWonWithoutHints;

        public int HighScore;
        public int TotalScore;

        public int CurrentWinStreak;
        public int BestWinStreak;

        [MemoryPackIgnore]
        public float AverageWinTime => TotalGamesWon > 0 ? TotalWinTime / TotalGamesWon : 0f;

        [MemoryPackIgnore]
        public float AverageWinMoves => TotalGamesWon > 0 ? (float)TotalWinMoves / TotalGamesWon : 0f;

        [MemoryPackIgnore]
        public float AverageScore => TotalGamesPlayed > 0 ? (float)TotalScore / TotalGamesPlayed : 0f;

        [MemoryPackIgnore]
        public float WinRate => TotalGamesPlayed > 0 ? (float)TotalGamesWon / TotalGamesPlayed : 0f;
    }
}
