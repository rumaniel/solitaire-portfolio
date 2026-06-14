using Model.Game;

namespace Service.BoardGameService
{
    public sealed class BoardGameServiceFactory : IBoardGameServiceFactory
    {
        private readonly PyramidGameService pyramid;
        private readonly TriPeaksGameService triPeaks;

        public BoardGameServiceFactory(PyramidGameService pyramid, TriPeaksGameService triPeaks)
        {
            this.pyramid = pyramid;
            this.triPeaks = triPeaks;
        }

        public IBoardGameService Create(GameType gameType)
            => gameType == GameType.TriPeaks ? triPeaks : pyramid;
    }
}
