using Model.Board;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class SelectionSnapshotTests
    {
        [Test]
        public void Empty_HasNoCellsAndNoWaste()
        {
            Assert.AreEqual(0, SelectionSnapshot.Empty.Cells.Count);
            Assert.IsFalse(SelectionSnapshot.Empty.WasteSelected);
        }

        [Test]
        public void Contains_FindsSelectedCell()
        {
            var s = new SelectionSnapshot(new[] { new CellId(3), new CellId(7) }, wasteSelected: false);
            Assert.IsTrue(s.Contains(new CellId(3)));
            Assert.IsTrue(s.Contains(new CellId(7)));
            Assert.IsFalse(s.Contains(new CellId(4)));
        }

        [Test]
        public void Equals_IsValueBased()
        {
            var a = new SelectionSnapshot(new[] { new CellId(1) }, true);
            var b = new SelectionSnapshot(new[] { new CellId(1) }, true);
            var c = new SelectionSnapshot(new[] { new CellId(1) }, false);
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void Ctor_DefensivelyCopies()
        {
            var src = new[] { new CellId(2) };
            var s = new SelectionSnapshot(src, false);
            src[0] = new CellId(9);
            Assert.IsTrue(s.Contains(new CellId(2)));
            Assert.IsFalse(s.Contains(new CellId(9)));
        }
    }
}
