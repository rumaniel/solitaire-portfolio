using System;
using Model.Stats;
using NUnit.Framework;
using Service.DailyService;

namespace Tests.EditMode
{
    [TestFixture]
    public class DailyShareTextTests
    {
        private const string Template =
            "Daily {date} | ⏱ {time} | Score {score} | {moves} moves | 🔥 {streak} day streak\n{url}";

        [Test]
        public void Build_ReplacesAllTokens()
        {
            var date = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);
            var session = new SessionStats { Score = 1240, MoveCount = 89, ElapsedSeconds = 154f };

            var text = DailyShareTextBuilder.Build(
                Template, "https://example.com/daily", date, session, streak: 5);

            StringAssert.Contains("2026-04-15", text);
            StringAssert.Contains("2:34", text);
            StringAssert.Contains("1240", text);
            StringAssert.Contains("89", text);
            StringAssert.Contains("5 day streak", text);
            StringAssert.Contains("https://example.com/daily", text);
        }

        [Test]
        public void Build_EmptyUrl_YieldsEmptyUrlToken()
        {
            var date = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);
            var session = new SessionStats { Score = 0, MoveCount = 0, ElapsedSeconds = 0f };

            var text = DailyShareTextBuilder.Build(Template, playUrl: null, date, session, streak: 0);

            StringAssert.DoesNotContain("{url}", text);
        }

        [Test]
        public void Build_EmptyTemplate_ReturnsEmpty()
        {
            var date = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);
            var session = new SessionStats();

            var text = DailyShareTextBuilder.Build(string.Empty, "", date, session, streak: 0);
            Assert.AreEqual(string.Empty, text);
        }

        [Test]
        public void Build_NullSession_UsesDefaults()
        {
            var date = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);

            var text = DailyShareTextBuilder.Build(Template, "", date, session: null, streak: 0);

            StringAssert.Contains("2026-04-15", text);
            StringAssert.Contains("Score 0", text);
            StringAssert.Contains("0 moves", text);
        }
    }
}
