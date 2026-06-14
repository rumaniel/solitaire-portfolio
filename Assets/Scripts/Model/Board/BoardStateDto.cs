using MemoryPack;
using Model.Card;

namespace Model.Board
{
    /// <summary>
    /// Serialization shape of <see cref="BoardState"/>. <see cref="Cells"/> is indexed by CellId.Value;
    /// a null entry means that cell has been removed. Stock/Waste are ordered bottom→top.
    /// </summary>
    [MemoryPackable]
    public partial class BoardStateDto
    {
        public CardDto[] Cells { get; set; }
        public CardDto[] Stock { get; set; }
        public CardDto[] Waste { get; set; }
        public int RecycleCount { get; set; }
    }
}
