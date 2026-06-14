using MemoryPack;
using Model.Game;
using Model.Stats;

namespace Model.Board
{
    /// <summary>Full board-game save record. Parallels <see cref="GameSnapshot"/> for the card stack.</summary>
    [MemoryPackable]
    public partial class BoardSnapshot
    {
        public GameType GameType { get; set; }
        public int Variant { get; set; }
        public int Seed { get; set; }
        public BoardStateDto CurrentState { get; set; }
        public BoardStateDto[] UndoHistory { get; set; }
        public SessionStatsDto Stats { get; set; }
        public long SavedAtUtcTicks { get; set; }
    }
}
