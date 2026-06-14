namespace Model.Stats
{
    public interface IScoreRule
    {
        int WasteToTableau { get; }
        int WasteToFoundation { get; }
        int TableauToFoundation { get; }
        int FoundationToTableau { get; }
        int TableauReveal { get; }
        int StockRecycle { get; }
    }
}
