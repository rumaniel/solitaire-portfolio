using Model.Stats;

namespace Tests.EditMode
{
    internal class StubScoreRule : IScoreRule
    {
        public int WasteToTableau { get; set; } = 5;
        public int WasteToFoundation { get; set; } = 10;
        public int TableauToFoundation { get; set; } = 10;
        public int FoundationToTableau { get; set; } = -15;
        public int TableauReveal { get; set; } = 5;
        public int StockRecycle { get; set; } = -100;
    }
}
