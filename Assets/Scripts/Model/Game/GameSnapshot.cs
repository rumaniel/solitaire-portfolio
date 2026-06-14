using MemoryPack;
using Model.Stats;

namespace Model.Game
{
    [MemoryPackable]
    public partial class GameSnapshot
    {
        public GameType GameType { get; set; }
        public int Seed { get; set; }
        public int DrawCount { get; set; }
        public TableStateDto CurrentState { get; set; }
        public TableStateDto[] UndoHistory { get; set; }
        public SessionStatsDto Stats { get; set; }
        public long SavedAtUtcTicks { get; set; }
    }
}
