using Model.App;

namespace Tests.EditMode
{
    internal class MockAppConfig : IAppConfig
    {
        public string DailyPlayUrl { get; set; } = "";
        public string ChallengePlayUrl { get; set; } = "";
        public string DailyShareTemplate { get; set; } = "{date}|{time}|{score}|{moves}|{streak}|{url}";
        public int DailyStatsHistoryLimit { get; set; } = 30;
        public string PrivacyPolicyUrl { get; set; } = "";
        public string AndroidStoreId { get; set; } = "";
        public string IosStoreId { get; set; } = "";
    }
}
