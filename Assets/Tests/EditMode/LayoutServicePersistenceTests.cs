using NUnit.Framework;
using R3;
using Service.LayoutService;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class LayoutServicePersistenceTests
    {
        private const string LeftHandedKey = "layout.left_handed";

        [SetUp]
        public void SetUp() => ClearKeys();

        [TearDown]
        public void TearDown() => ClearKeys();

        [Test]
        public void IsLeftHanded_DefaultsFalse_WhenNoPrefs()
        {
            Assert.IsFalse(new LayoutService().IsLeftHanded);
        }

        [Test]
        public void SetLeftHanded_PersistsAcrossInstances()
        {
            new LayoutService().SetLeftHanded(true);
            Assert.IsTrue(new LayoutService().IsLeftHanded);
        }

        [Test]
        public void SetLeftHanded_FiresObservableOnChange()
        {
            var svc = new LayoutService();
            bool? captured = null;
            using var sub = svc.OnLeftHandedChanged.Subscribe(v => captured = v);

            svc.SetLeftHanded(true);

            Assert.IsTrue(captured.HasValue);
            Assert.IsTrue(captured.Value);
        }

        [Test]
        public void SetLeftHanded_DoesNotFire_WhenValueUnchanged()
        {
            var svc = new LayoutService();
            int fireCount = 0;
            using var sub = svc.OnLeftHandedChanged.Subscribe(_ => fireCount++);

            svc.SetLeftHanded(false);

            Assert.AreEqual(0, fireCount);
        }

        private static void ClearKeys()
        {
            PlayerPrefs.DeleteKey(LeftHandedKey);
            PlayerPrefs.Save();
        }
    }
}
