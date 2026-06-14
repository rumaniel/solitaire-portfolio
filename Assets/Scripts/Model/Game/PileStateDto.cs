using MemoryPack;
using Model.Card;

namespace Model.Game
{
    [MemoryPackable]
    public partial class PileStateDto
    {
        public PileType PileType { get; set; }
        public int PileIndex { get; set; }
        public CardDto[] Cards { get; set; }
        public int FaceUpFromIndex { get; set; }
    }
}
