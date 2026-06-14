using Model.Skin;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class SkinIdTests
    {
        [Test]
        public void Equals_SameValue_AreEqualWithSameHash()
        {
            Assert.AreEqual(new SkinId("classic"), new SkinId("classic"));
            Assert.AreEqual(new SkinId("classic").GetHashCode(), new SkinId("classic").GetHashCode());
        }

        [Test]
        public void Equals_DifferentValue_AreNotEqual()
        {
            Assert.AreNotEqual(new SkinId("classic"), new SkinId("dark"));
        }

        [Test]
        public void IsEmpty_TrueForNullOrEmpty()
        {
            Assert.IsTrue(new SkinId(null).IsEmpty);
            Assert.IsTrue(new SkinId("").IsEmpty);
            Assert.IsFalse(new SkinId("classic").IsEmpty);
        }
    }
}
