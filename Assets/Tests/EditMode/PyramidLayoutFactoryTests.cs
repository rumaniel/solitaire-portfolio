using Model.Board;
using Model.Game;
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class PyramidLayoutFactoryTests
    {
        [Test]
        public void Creates28CellsOverSevenRows_ForPyramid()
        {
            var layout = PyramidLayoutFactory.Create();
            Assert.AreEqual(GameType.Pyramid, layout.GameType);
            Assert.AreEqual(28, layout.Count);
        }

        [Test]
        public void Apex_IsCoveredByTopOfRowTwo()
        {
            var layout = PyramidLayoutFactory.Create();
            var apex = layout.Cell(new CellId(0));
            Assert.AreEqual(2, apex.CoverBlockers.Count);
            Assert.Contains(new CellId(1), (System.Collections.ICollection)apex.CoverBlockers);
            Assert.Contains(new CellId(2), (System.Collections.ICollection)apex.CoverBlockers);
        }

        [Test]
        public void BottomRowCells_HaveNoCover()
        {
            var layout = PyramidLayoutFactory.Create();
            // bottom row (row 6) = indices 21..27
            for (int i = 21; i <= 27; i++)
                Assert.AreEqual(0, layout.Cell(new CellId(i)).CoverBlockers.Count, $"cell {i}");
        }
    }
}
