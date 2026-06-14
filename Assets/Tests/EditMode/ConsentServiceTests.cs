using NUnit.Framework;
using Service.ConsentService;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class ConsentServiceTests
    {
        private const string AcceptedVersionKey = "consent.policy_version_accepted";

        [SetUp]
        public void SetUp() => ClearKeys();

        [TearDown]
        public void TearDown() => ClearKeys();

        [Test]
        public void FreshService_WithoutPrefs_NeedsConsent()
        {
            var svc = new ConsentService();
            Assert.IsTrue(svc.NeedsConsent);
            Assert.AreEqual(0, svc.AcceptedVersion);
        }

        [Test]
        public void MarkAccepted_PersistsAcrossInstances()
        {
            new ConsentService().MarkAccepted();
            Assert.IsFalse(new ConsentService().NeedsConsent);
        }

        [Test]
        public void MarkAccepted_StoresCurrentPolicyVersion()
        {
            var svc = new ConsentService();
            svc.MarkAccepted();
            Assert.AreEqual(svc.PolicyVersion, svc.AcceptedVersion);
            Assert.AreEqual(ConsentService.CurrentPolicyVersion, svc.AcceptedVersion);
        }

        [Test]
        public void LowerStoredVersion_TriggersReprompt()
        {
            new ConsentService().MarkAccepted();
            // 정책 버전 bump 시뮬레이션 — 저장된 버전을 0으로 다운그레이드.
            PlayerPrefs.SetInt(AcceptedVersionKey, 0);
            PlayerPrefs.Save();

            Assert.IsTrue(new ConsentService().NeedsConsent);
        }

        private static void ClearKeys()
        {
            PlayerPrefs.DeleteKey(AcceptedVersionKey);
            PlayerPrefs.Save();
        }
    }
}
