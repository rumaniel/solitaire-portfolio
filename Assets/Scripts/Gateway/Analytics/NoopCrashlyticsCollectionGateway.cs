namespace Gateway.Analytics
{
    /// <summary>No-op fallback for platforms without Firebase Crashlytics (WebGL — Editor uses the Firebase impl since the SDK works there). Calls are swallowed silently.</summary>
    public class NoopCrashlyticsCollectionGateway : ICrashlyticsCollectionGateway
    {
        public void SetCollectionEnabled(bool enabled) { }
    }
}
