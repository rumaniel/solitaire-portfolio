#if !UNITY_WEBGL
using Firebase.Crashlytics;

namespace Gateway.Analytics
{
    /// <summary>Wraps the Firebase Crashlytics auto-collection toggle. The Android manifest defaults it to false (`firebase_crashlytics_collection_enabled=false` in FirebaseAnalyticsConsent.androidlib); this gateway is the single runtime opt-in path after consent is granted.</summary>
    public class FirebaseCrashlyticsCollectionGateway : ICrashlyticsCollectionGateway
    {
        public void SetCollectionEnabled(bool enabled)
        {
            Crashlytics.IsCrashlyticsCollectionEnabled = enabled;
        }
    }
}
#endif
