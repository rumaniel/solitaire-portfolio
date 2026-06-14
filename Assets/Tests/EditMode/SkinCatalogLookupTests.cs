using System.Collections.Generic;
using System.Text.RegularExpressions;
using Data.Skin;
using Model.Skin;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    [TestFixture]
    public class SkinCatalogLookupTests
    {
        private static SkinCatalogEntry Entry(string id)
            => new SkinCatalogEntry(id, $"skin.{id}", null, new CardSpriteSetReference($"{id}-guid"));

        [Test]
        public void TryGet_ReturnsEntry_ForKnownId()
        {
            var lookup = new SkinCatalogLookup(new List<SkinCatalogEntry> { Entry("classic"), Entry("dark") });
            Assert.IsTrue(lookup.TryGet(new SkinId("dark"), out var entry));
            Assert.AreEqual("dark", entry.Id.Value);
        }

        [Test]
        public void TryGet_ReturnsFalse_ForUnknownId()
        {
            var lookup = new SkinCatalogLookup(new List<SkinCatalogEntry> { Entry("classic") });
            Assert.IsFalse(lookup.TryGet(new SkinId("ghost"), out _));
        }

        [Test]
        public void Contains_MatchesTryGet()
        {
            var lookup = new SkinCatalogLookup(new List<SkinCatalogEntry> { Entry("classic") });
            Assert.IsTrue(lookup.Contains(new SkinId("classic")));
            Assert.IsFalse(lookup.Contains(new SkinId("dark")));
        }

        [Test]
        public void Skins_PreservesOrder()
        {
            var lookup = new SkinCatalogLookup(new List<SkinCatalogEntry> { Entry("classic"), Entry("dark"), Entry("neon") });
            Assert.AreEqual(3, lookup.Skins.Count);
            Assert.AreEqual("classic", lookup.Skins[0].Id.Value);
            Assert.AreEqual("neon", lookup.Skins[2].Id.Value);
        }

        [Test]
        public void DuplicateId_FirstWins_AndWarns()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Duplicate skin id"));
            var lookup = new SkinCatalogLookup(new List<SkinCatalogEntry> { Entry("classic"), Entry("classic") });
            Assert.AreEqual(1, lookup.Skins.Count);
        }
    }
}
