using Model.Board;

namespace Service.BoardGameService
{
    /// <summary>Pyramid scoring: fixed points per cleared card, a one-off board-cleared bonus, and
    /// per-draw / per-recycle efficiency penalties (tighter play scores higher).</summary>
    public sealed class PyramidScoreRule : IBoardScoreRule
    {
        private readonly int perCard;

        public PyramidScoreRule(int perCard = 5, int boardClearedBonus = 100,
            int stockDrawPenalty = -2, int recyclePenalty = -10)
        {
            this.perCard = perCard;
            BoardClearedBonus = boardClearedBonus;
            ScoreForStockDraw = stockDrawPenalty;
            ScoreForRecycle = recyclePenalty;
        }

        public int ScoreForRemoval(int cardCount) => cardCount * perCard;
        public int BoardClearedBonus { get; }
        public int ScoreForStockDraw { get; }
        public int ScoreForRecycle { get; }
    }
}
