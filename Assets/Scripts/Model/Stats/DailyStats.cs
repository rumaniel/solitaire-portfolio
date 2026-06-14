using System.Collections.Generic;
using MemoryPack;

namespace Model.Stats
{
    [MemoryPackable]
    public partial class DailyStats
    {
        public int CurrentStreak;
        public int BestStreak;
        public int TotalCompleted;
        public int TotalAttempted;
        public string LastCompletedDateKey;
        public List<DailyRecord> History;

        public DailyStats()
        {
            History = new List<DailyRecord>();
        }
    }

    [MemoryPackable]
    public partial class DailyRecord
    {
        public string DateKey;
        public bool Won;
        public int Score;
        public int MoveCount;
        public float ElapsedSeconds;
    }
}
