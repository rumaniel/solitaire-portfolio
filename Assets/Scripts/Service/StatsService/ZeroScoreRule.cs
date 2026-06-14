using Model.Stats;

namespace Service.StatsService
{
    /// <summary>
    /// Null-object <see cref="IScoreRule"/> for board games (Pyramid/TriPeaks), which score via
    /// <see cref="ISessionStatsService.RecordScoreDelta"/> and never use the pile-type scoring path.
    /// </summary>
    public sealed class ZeroScoreRule : IScoreRule
    {
        public int WasteToTableau => 0;
        public int WasteToFoundation => 0;
        public int TableauToFoundation => 0;
        public int FoundationToTableau => 0;
        public int TableauReveal => 0;
        public int StockRecycle => 0;
    }
}
