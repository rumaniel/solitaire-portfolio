using MemoryPack;

namespace Model.Game
{
    [MemoryPackable]
    public partial class TableStateDto
    {
        public PileStateDto Stock { get; set; }
        public PileStateDto Waste { get; set; }
        public PileStateDto[] Foundations { get; set; }
        public PileStateDto[] Tableaus { get; set; }
        public int WasteFanCount { get; set; }
    }
}
