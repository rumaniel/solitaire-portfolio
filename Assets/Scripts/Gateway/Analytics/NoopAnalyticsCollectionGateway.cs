namespace Gateway.Analytics
{
    /// <summary>No-op fallback for platforms without Firebase Analytics (WebGL — Editor uses the Firebase impl since the SDK works there). Calls are swallowed silently.</summary>
    public class NoopAnalyticsCollectionGateway : IAnalyticsCollectionGateway
    {
        public void SetCollectionEnabled(bool enabled) { }
    }
}
