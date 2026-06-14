using NUnit.Framework;
using R3;
using Service.HapticService;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    public class HapticServiceTests
    {
        private const string EnabledKey = "haptic.enabled";

        private class CapturingBridge : IVibratorBridge
        {
            public int CallCount;
            public int LastDuration;
            public int LastAmplitude;

            public void Vibrate(int durationMs, int amplitude)
            {
                CallCount++;
                LastDuration = durationMs;
                LastAmplitude = amplitude;
            }

            public void Dispose() { }
        }

        [SetUp]
        public void SetUp() => ClearKeys();

        [TearDown]
        public void TearDown() => ClearKeys();

        [Test]
        public void IsEnabled_DefaultsTrue_WhenNoPrefs()
        {
            var svc = new HapticService(new CapturingBridge());
            Assert.IsTrue(svc.IsEnabled);
        }

        [Test]
        public void SetEnabled_PersistsAcrossInstances()
        {
            new HapticService(new CapturingBridge()).SetEnabled(false);
            Assert.IsFalse(new HapticService(new CapturingBridge()).IsEnabled);
        }

        [Test]
        public void Trigger_WhenDisabled_SkipsBridge()
        {
            var bridge = new CapturingBridge();
            var svc = new HapticService(bridge);
            svc.SetEnabled(false);

            svc.Trigger(HapticTier.Heavy);

            Assert.AreEqual(0, bridge.CallCount);
        }

        [Test]
        public void Trigger_WhenEnabled_PassesTierParamsToBridge()
        {
            var bridge = new CapturingBridge();
            var svc = new HapticService(bridge);

            svc.Trigger(HapticTier.Light);
            Assert.AreEqual(1, bridge.CallCount);
            Assert.AreEqual(10, bridge.LastDuration);
            Assert.AreEqual(80, bridge.LastAmplitude);

            svc.Trigger(HapticTier.Heavy);
            Assert.AreEqual(2, bridge.CallCount);
            Assert.AreEqual(40, bridge.LastDuration);
            Assert.AreEqual(255, bridge.LastAmplitude);

            svc.Trigger(HapticTier.Selection);
            Assert.AreEqual(3, bridge.CallCount);
            Assert.AreEqual(5, bridge.LastDuration);
            Assert.AreEqual(50, bridge.LastAmplitude);
        }

        [Test]
        public void SetEnabled_FiresObservableOnChange()
        {
            var svc = new HapticService(new CapturingBridge());
            bool? captured = null;
            using var sub = svc.OnEnabledChanged.Subscribe(v => captured = v);

            svc.SetEnabled(false);

            Assert.IsTrue(captured.HasValue);
            Assert.IsFalse(captured.Value);
        }

        [Test]
        public void SetEnabled_DoesNotFireObservable_WhenValueUnchanged()
        {
            var svc = new HapticService(new CapturingBridge());
            int fireCount = 0;
            using var sub = svc.OnEnabledChanged.Subscribe(_ => fireCount++);

            svc.SetEnabled(true);

            Assert.AreEqual(0, fireCount);
        }

        private static void ClearKeys()
        {
            PlayerPrefs.DeleteKey(EnabledKey);
            PlayerPrefs.Save();
        }
    }
}
