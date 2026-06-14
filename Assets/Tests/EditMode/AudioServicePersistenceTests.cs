using NUnit.Framework;
using Service.AudioService;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class AudioServicePersistenceTests
    {
        private const string MusicMutedKey = "audio.music.muted";
        private const string SfxMutedKey = "audio.sfx.muted";

        [SetUp]
        public void SetUp() => ClearKeys();

        [TearDown]
        public void TearDown() => ClearKeys();

        [Test]
        public void FreshService_WithoutPrefs_StartsUnmuted()
        {
            var svc = new AudioService();
            Assert.IsFalse(svc.IsMusicMuted);
            Assert.IsFalse(svc.IsSfxMuted);
        }

        [Test]
        public void MusicMuted_PersistsAcrossInstances()
        {
            new AudioService().SetMusicMuted(true);
            Assert.IsTrue(new AudioService().IsMusicMuted);
        }

        [Test]
        public void SfxMuted_PersistsAcrossInstances()
        {
            new AudioService().SetSfxMuted(true);
            Assert.IsTrue(new AudioService().IsSfxMuted);
        }

        [Test]
        public void Unmute_PersistsAcrossInstances()
        {
            var first = new AudioService();
            first.SetMusicMuted(true);
            first.SetMusicMuted(false);
            Assert.IsFalse(new AudioService().IsMusicMuted);
        }

        [Test]
        public void MusicAndSfx_AreIndependentlyPersisted()
        {
            new AudioService().SetMusicMuted(true);
            var reloaded = new AudioService();
            Assert.IsTrue(reloaded.IsMusicMuted);
            Assert.IsFalse(reloaded.IsSfxMuted);
        }

        private static void ClearKeys()
        {
            PlayerPrefs.DeleteKey(MusicMutedKey);
            PlayerPrefs.DeleteKey(SfxMutedKey);
            // Flush so the editor's shared PlayerPrefs store can't leak state
            // into other tests or subsequent play-mode runs in the same session.
            PlayerPrefs.Save();
        }
    }
}
