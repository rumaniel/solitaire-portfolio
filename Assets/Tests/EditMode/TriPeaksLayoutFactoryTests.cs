using System.Collections;
using System.Linq;
using Model.Board;
using Model.Game;
using NUnit.Framework;
using Service.BoardGameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class TriPeaksLayoutFactoryTests
    {
        [Test]
        public void Creates28Cells_ForTriPeaks()
        {
            var layout = TriPeaksLayoutFactory.Create();
            Assert.AreEqual(GameType.TriPeaks, layout.GameType);
            Assert.AreEqual(28, layout.Count);
        }

        [Test]
        public void ApexCellIds_AreTheThreeRow0Tips()
        {
            CollectionAssert.AreEquivalent(
                new[] { new CellId(0), new CellId(1), new CellId(2) },
                TriPeaksLayoutFactory.ApexCellIds);
        }

        [Test]
        public void Apex0_CoveredByRow1Cells3And4()
        {
            var layout = TriPeaksLayoutFactory.Create();
            var apex = layout.Cell(new CellId(0));
            Assert.AreEqual(2, apex.CoverBlockers.Count);
            Assert.Contains(new CellId(3), (ICollection)apex.CoverBlockers);
            Assert.Contains(new CellId(4), (ICollection)apex.CoverBlockers);
        }

        [Test]
        public void BaseRow_HasTenCellsWithNoCover()
        {
            var layout = TriPeaksLayoutFactory.Create();
            for (int id = 18; id <= 27; id++)
                Assert.AreEqual(0, layout.Cell(new CellId(id)).CoverBlockers.Count, $"cell {id}");
        }

        [Test]
        public void Row2Cell9_CoveredByBaseCells18And19()
        {
            var layout = TriPeaksLayoutFactory.Create();
            var cell = layout.Cell(new CellId(9));
            Assert.Contains(new CellId(18), (ICollection)cell.CoverBlockers);
            Assert.Contains(new CellId(19), (ICollection)cell.CoverBlockers);
        }

        [Test]
        public void Constructs_WithoutThrowing_AllBlockersValidAndDense()
        {
            Assert.DoesNotThrow(() => TriPeaksLayoutFactory.Create());
            var layout = TriPeaksLayoutFactory.Create();
            Assert.AreEqual(28, layout.Cells.Select(c => c.Id).Distinct().Count());
        }
    }
}
