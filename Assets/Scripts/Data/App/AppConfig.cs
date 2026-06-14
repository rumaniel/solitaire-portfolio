using Model.App;
using UnityEngine;

namespace Data.App
{
    /// <summary>
    /// App-wide configuration ScriptableObject. Implements <see cref="IAppConfig"/>
    /// so Service-layer code can consume it without referencing the Data assembly.
    /// </summary>
    [CreateAssetMenu(menuName = "Solitaire/App/App Config", fileName = "AppConfig")]
    public class AppConfig : ScriptableObject, IAppConfig
    {
        [Header("URLs")]
        [Tooltip("Daily 공유 텍스트의 {url} 토큰에 삽입. 비워두면 빈 문자열로 치환.")]
        [SerializeField] private string dailyPlayUrl = "";

        [Tooltip("Challenge 공유 텍스트의 {url} 토큰에 삽입. 비워두면 빈 문자열로 치환.")]
        [SerializeField] private string challengePlayUrl = "";

        [Header("Daily Stats")]
        [Tooltip("DailyStats.History에 보관할 최근 기록 개수.")]
        [SerializeField] private int dailyStatsHistoryLimit = 30;

        [Header("Privacy & Store")]
        [Tooltip("Settings 패널의 Privacy Policy 버튼이 여는 URL. 비워두면 버튼 비활성.")]
        [SerializeField] private string privacyPolicyUrl = "";

        [Tooltip("Google Play 패키지 id (예: com.example.solitaire). 비워두면 Rate 버튼 비활성.")]
        [SerializeField] private string androidStoreId = "";

        [Tooltip("App Store numeric id (예: 1234567890). 비워두면 Rate 버튼 비활성.")]
        [SerializeField] private string iosStoreId = "";

        public string DailyPlayUrl => dailyPlayUrl;
        public string ChallengePlayUrl => challengePlayUrl;
        public int DailyStatsHistoryLimit => dailyStatsHistoryLimit;
        public string PrivacyPolicyUrl => privacyPolicyUrl;
        public string AndroidStoreId => androidStoreId;
        public string IosStoreId => iosStoreId;
    }
}
