#if !UNITY_WEBGL
using Firebase.Analytics;

namespace Gateway.Analytics
{
    /// <summary>Wraps the Firebase Analytics auto-collection toggle. The Android manifest defaults it to false (`firebase_analytics_collection_enabled=false`); this gateway is the single runtime opt-in path after consent is granted.</summary>
    public class FirebaseAnalyticsCollectionGateway : IAnalyticsCollectionGateway
    {
        public void SetCollectionEnabled(bool enabled)
        {
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(enabled);
        }
    }
}
#endif
