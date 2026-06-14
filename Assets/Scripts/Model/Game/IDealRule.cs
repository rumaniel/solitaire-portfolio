namespace Model.Game
{
    public interface IDealRule
    {
        int TableauCount { get; }
        int FoundationCount { get; }
        int PerSuitCardCount { get; }
        bool HasWaste { get; }
        bool CanRecycleStock { get; }
        int StockDrawCount { get; }
        bool StockDealsToTableau { get; }
        int[] InitialCardCounts { get; }
        int InitialFaceUpPerColumn { get; }
        bool OnlyKingOnEmptyTableau { get; }

        // Spider additions. Default implementations keep every pre-Spider rule
        // (assets, test stubs, the frozen daily rule) source-identical.
        int DeckCount => 1;
        int SuitCount => 4;
        TableauRunRule RunRule => TableauRunRule.AlternatingColor;
        TableauDropRule DropRule => TableauDropRule.AlternatingColor;
        bool StockDealRequiresNoEmptyColumn => false;
        bool AutoCollectCompletedRuns => false;
    }
}
