using MemoryPack;

namespace Model.Stats
{
    [MemoryPackable]
    public partial class SessionStatsDto
    {
        public int Score { get; set; }
        public int MoveCount { get; set; }
        public float ElapsedSeconds { get; set; }
        public bool UndoUsed { get; set; }
        public bool HintUsed { get; set; }
        public int HintCount { get; set; }
    }
}
