using System.IO;
using System.Xml;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// FirebaseAnalyticsConsent.androidlib 의 소스 manifest 가 consent 게이트 계약을 유지하는지 검증.
    /// Android Gradle manifest-merger 는 이 lib 의 선언을 final AAB 에 합쳐주므로, 소스 계약이
    /// 깨지지 않는 한 merged manifest 도 같은 결과를 보장한다.
    /// </summary>
    [TestFixture]
    public class AndroidManifestConsentTests
    {
        private const string ManifestRelativePath =
            "Plugins/Android/FirebaseAnalyticsConsent.androidlib/AndroidManifest.xml";

        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string ToolsNs = "http://schemas.android.com/tools";

        private XmlDocument doc;

        [SetUp]
        public void SetUp()
        {
            var path = Path.Combine(Application.dataPath, ManifestRelativePath);
            Assert.IsTrue(File.Exists(path), $"Consent manifest not found at {path}");
            doc = new XmlDocument();
            doc.Load(path);
        }

        [Test]
        public void AdIdPermission_IsStripped()
        {
            var node = FindUsesPermission("com.google.android.gms.permission.AD_ID");
            Assert.IsNotNull(node, "AD_ID uses-permission entry must exist with tools:node=\"remove\".");

            var toolsNode = node.Attributes?["node", ToolsNs]?.Value;
            Assert.AreEqual("remove", toolsNode,
                "AD_ID permission must declare tools:node=\"remove\" so the merger strips the transitive permission added by play-services-ads-identifier.");
        }

        [Test]
        public void AnalyticsAutoCollection_DefaultsOff()
        {
            AssertMetaDataFalse("firebase_analytics_collection_enabled");
        }

        [Test]
        public void CrashlyticsAutoCollection_DefaultsOff()
        {
            AssertMetaDataFalse("firebase_crashlytics_collection_enabled");
        }

        [Test]
        public void AdidCollection_DisabledByGaFlag()
        {
            AssertMetaDataFalse("google_analytics_adid_collection_enabled");
        }

        private XmlNode FindUsesPermission(string permissionName)
        {
            foreach (XmlNode node in doc.GetElementsByTagName("uses-permission"))
            {
                if (node.Attributes?["name", AndroidNs]?.Value == permissionName) return node;
            }
            return null;
        }

        private void AssertMetaDataFalse(string metaDataName)
        {
            XmlNode found = null;
            foreach (XmlNode node in doc.GetElementsByTagName("meta-data"))
            {
                if (node.Attributes?["name", AndroidNs]?.Value == metaDataName)
                {
                    found = node;
                    break;
                }
            }
            Assert.IsNotNull(found, $"meta-data '{metaDataName}' must exist in the consent manifest.");
            Assert.AreEqual("false", found.Attributes?["value", AndroidNs]?.Value,
                $"meta-data '{metaDataName}' must default to \"false\" so the SDK stays off until consent is granted.");
        }
    }
}
