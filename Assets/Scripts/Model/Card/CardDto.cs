using MemoryPack;

namespace Model.Card
{
    [MemoryPackable]
    public partial class CardDto
    {
        public Rank Rank { get; set; }
        public Suit Suit { get; set; }
    }
}
