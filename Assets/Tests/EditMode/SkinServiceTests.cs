using System.Text.RegularExpressions;
using Data.Skin;
using Model.Skin;
using NUnit.Framework;
using Service.SkinService;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    [TestFixture]
    public class SkinServiceTests
    {
        private static readonly SkinId Classic = new SkinId("classic");
        private static readonly SkinId Dark = new SkinId("dark");

        private FakeSkinCatalog catalog;
        private FakeSkinAssetGateway gateway;
        private CardSpriteSetReference classicRef;
        private CardSpriteSetReference darkRef;

        [SetUp]
        public void SetUp()
        {
            classicRef = new CardSpriteSetReference("classic-guid");
            darkRef = new CardSpriteSetReference("dark-guid");
            catalog = new FakeSkinCatalog()
                .Add(new SkinCatalogEntry("classic", "skin.classic", null, classicRef))
                .Add(new SkinCatalogEntry("dark", "skin.dark", null, darkRef));
            gateway = new FakeSkinAssetGateway();
        }

        private SkinService NewService(ISkinPreferenceStore prefs) => new SkinService(catalog, gateway, prefs);

        [Test]
        public void Initialize_DefaultsToClassic_WhenNoPreference()
        {
            var svc = NewService(new InMemorySkinPreferenceStore());
            svc.InitializeAsync().GetAwaiter().GetResult();
            Assert.AreEqual(Classic, svc.CurrentSkinId.CurrentValue);
            Assert.IsNotNull(svc.CurrentSpriteSet.CurrentValue);
        }

        [Test]
        public void Initialize_RestoresSavedSkin()
        {
            var svc = NewService(new InMemorySkinPreferenceStore(Dark));
            svc.InitializeAsync().GetAwaiter().GetResult();
            Assert.AreEqual(Dark, svc.CurrentSkinId.CurrentValue);
        }

        [Test]
        public void Initialize_FallsBackToClassic_WhenSavedSkinMissingFromCatalog()
        {
            var svc = NewService(new InMemorySkinPreferenceStore(new SkinId("ghost")));
            LogAssert.Expect(LogType.Warning, new Regex("not in catalog"));
            svc.InitializeAsync().GetAwaiter().GetResult();
            Assert.AreEqual(Classic, svc.CurrentSkinId.CurrentValue);
        }

        [Test]
        public void SelectSkin_UpdatesCurrentSkinAndSpriteSet()
        {
            var svc = NewService(new InMemorySkinPreferenceStore());
            svc.InitializeAsync().GetAwaiter().GetResult();
            var classicSet = svc.CurrentSpriteSet.CurrentValue;

            svc.SelectSkinAsync(Dark).GetAwaiter().GetResult();

            Assert.AreEqual(Dark, svc.CurrentSkinId.CurrentValue);
            Assert.AreNotSame(classicSet, svc.CurrentSpriteSet.CurrentValue);
        }

        [Test]
        public void SelectSkin_ReleasesPreviousReference()
        {
            var svc = NewService(new InMemorySkinPreferenceStore());
            svc.InitializeAsync().GetAwaiter().GetResult();

            svc.SelectSkinAsync(Dark).GetAwaiter().GetResult();

            CollectionAssert.Contains(gateway.Released, classicRef);
        }

        [Test]
        public void SelectSkin_PersistsSelection()
        {
            var prefs = new InMemorySkinPreferenceStore();
            var svc = NewService(prefs);
            svc.InitializeAsync().GetAwaiter().GetResult();

            svc.SelectSkinAsync(Dark).GetAwaiter().GetResult();

            Assert.AreEqual(1, prefs.SaveCalls);
            Assert.IsTrue(prefs.TryLoad(out var saved));
            Assert.AreEqual(Dark, saved);
        }

        [Test]
        public void SelectSkin_SameId_IsNoOp()
        {
            var svc = NewService(new InMemorySkinPreferenceStore());
            svc.InitializeAsync().GetAwaiter().GetResult();
            int loadsAfterInit = gateway.LoadCalls;

            svc.SelectSkinAsync(Classic).GetAwaiter().GetResult();

            Assert.AreEqual(loadsAfterInit, gateway.LoadCalls);
            Assert.IsEmpty(gateway.Released);
        }
    }
}
