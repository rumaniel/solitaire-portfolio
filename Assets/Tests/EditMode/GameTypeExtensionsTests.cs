using Model.Game;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class GameTypeExtensionsTests
    {
        [Test]
        public void IsBoardMode_TrueForLayeredBoardGames()
        {
            Assert.IsTrue(GameType.Pyramid.IsBoardMode());
            Assert.IsTrue(GameType.TriPeaks.IsBoardMode());
        }

        [Test]
        public void IsBoardMode_FalseForCardGames()
        {
            Assert.IsFalse(GameType.Klondike.IsBoardMode());
            Assert.IsFalse(GameType.Easthaven.IsBoardMode());
            Assert.IsFalse(GameType.Spider.IsBoardMode());
            Assert.IsFalse(GameType.None.IsBoardMode());
        }
    }
}
