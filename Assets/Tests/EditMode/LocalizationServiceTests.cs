using NUnit.Framework;
using Service.LocalizationService;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>EditMode-safe tests — only the pure parts that don't spin up LocalizationSettings.</summary>
    [TestFixture]
    public class LocalizationServiceTests
    {
        [Test]
        public void MapSystemLanguage_MapsKorean()
            => Assert.AreEqual("ko", LocalizationService.MapSystemLanguage(SystemLanguage.Korean));

        [Test]
        public void MapSystemLanguage_MapsJapanese()
            => Assert.AreEqual("ja", LocalizationService.MapSystemLanguage(SystemLanguage.Japanese));

        [Test]
        public void MapSystemLanguage_MapsChineseSimplified()
            => Assert.AreEqual("zh-Hans", LocalizationService.MapSystemLanguage(SystemLanguage.ChineseSimplified));

        [Test]
        public void MapSystemLanguage_MapsChineseTraditional()
            => Assert.AreEqual("zh-Hant", LocalizationService.MapSystemLanguage(SystemLanguage.ChineseTraditional));

        [Test]
        public void MapSystemLanguage_FallsBackToEnglish()
        {
            Assert.AreEqual("en", LocalizationService.MapSystemLanguage(SystemLanguage.English));
            Assert.AreEqual("en", LocalizationService.MapSystemLanguage(SystemLanguage.Unknown));
            Assert.AreEqual("en", LocalizationService.MapSystemLanguage(SystemLanguage.Vietnamese));
            Assert.AreEqual("en", LocalizationService.MapSystemLanguage(SystemLanguage.Arabic));
        }

        [Test]
        public void PlayerPrefsRoundTrip()
        {
            PlayerPrefs.DeleteKey(LocalizationService.PlayerPrefsLocaleKey);
            Assert.IsEmpty(PlayerPrefs.GetString(LocalizationService.PlayerPrefsLocaleKey, string.Empty));

            PlayerPrefs.SetString(LocalizationService.PlayerPrefsLocaleKey, "ko");
            Assert.AreEqual("ko", PlayerPrefs.GetString(LocalizationService.PlayerPrefsLocaleKey, string.Empty));

            PlayerPrefs.DeleteKey(LocalizationService.PlayerPrefsLocaleKey);
        }
    }
}
