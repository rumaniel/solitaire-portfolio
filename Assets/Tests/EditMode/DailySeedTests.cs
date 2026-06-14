using System;
using Model.Game;
using NUnit.Framework;
using Service.GameService;

namespace Tests.EditMode
{
    [TestFixture]
    public class DailySeedTests
    {
        [Test]
        public void For_SameDateAndType_ReturnsSameSeed()
        {
            var date = new DateTime(2026, 4, 15, 12, 34, 56, DateTimeKind.Utc);
            var a = DailySeed.For(date, GameType.Klondike);
            var b = DailySeed.For(date, GameType.Klondike);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void For_IgnoresTimeOfDay()
        {
            var morning = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);
            var evening = new DateTime(2026, 4, 15, 23, 59, 59, DateTimeKind.Utc);
            Assert.AreEqual(
                DailySeed.For(morning, GameType.Klondike),
                DailySeed.For(evening, GameType.Klondike));
        }

        [Test]
        public void For_DifferentGameType_ReturnsDifferentSeed()
        {
            var date = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);
            var klondike = DailySeed.For(date, GameType.Klondike);
            var easthaven = DailySeed.For(date, GameType.Easthaven);
            Assert.AreNotEqual(klondike, easthaven);
        }

        [Test]
        public void For_DifferentDate_ReturnsDifferentSeed()
        {
            var d1 = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);
            var d2 = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreNotEqual(
                DailySeed.For(d1, GameType.Klondike),
                DailySeed.For(d2, GameType.Klondike));
        }

        [Test]
        public void DateKey_FormatsAsYyyyMmDd()
        {
            var date = new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual("2026-04-15", DailySeed.DateKey(date));
        }

        [Test]
        public void DateKey_IgnoresTimeOfDay()
        {
            var midnight = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);
            var nearMidnight = new DateTime(2026, 4, 15, 23, 59, 59, DateTimeKind.Utc);
            Assert.AreEqual(DailySeed.DateKey(midnight), DailySeed.DateKey(nearMidnight));
        }
    }
}
