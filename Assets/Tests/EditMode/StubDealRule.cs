using Model.Game;

namespace Tests.EditMode
{
    internal class StubDealRule : IDealRule
    {
        public int TableauCount { get; set; } = 7;
        public int FoundationCount { get; set; } = 4;
        public int PerSuitCardCount { get; set; } = 13;
        public bool HasWaste { get; set; } = true;
        public bool CanRecycleStock { get; set; } = true;
        public int StockDrawCount { get; set; } = 1;
        public bool StockDealsToTableau { get; set; } = false;
        public int[] InitialCardCounts { get; set; } = new[] { 1, 2, 3, 4, 5, 6, 7 };
        public int InitialFaceUpPerColumn { get; set; } = 1;
        public bool OnlyKingOnEmptyTableau { get; set; } = true;

        public int DeckCountOverride { get; set; } = 1;
        public int SuitCountOverride { get; set; } = 4;
        public TableauRunRule RunRuleOverride { get; set; } = TableauRunRule.AlternatingColor;
        public TableauDropRule DropRuleOverride { get; set; } = TableauDropRule.AlternatingColor;
        public bool StockDealRequiresNoEmptyColumnOverride { get; set; }
        public bool AutoCollectCompletedRunsOverride { get; set; }

        public int DeckCount => DeckCountOverride;
        public int SuitCount => SuitCountOverride;
        public TableauRunRule RunRule => RunRuleOverride;
        public TableauDropRule DropRule => DropRuleOverride;
        public bool StockDealRequiresNoEmptyColumn => StockDealRequiresNoEmptyColumnOverride;
        public bool AutoCollectCompletedRuns => AutoCollectCompletedRunsOverride;
    }
}
