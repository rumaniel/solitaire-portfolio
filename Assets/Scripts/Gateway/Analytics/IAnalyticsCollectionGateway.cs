namespace Gateway.Analytics
{
    /// <summary>Controls Firebase Analytics auto-collection (events fired by the SDK without explicit LogEvent calls). Default is off via AndroidManifest meta-data; opt-in once user consent is granted.</summary>
    public interface IAnalyticsCollectionGateway
    {
        void SetCollectionEnabled(bool enabled);
    }
}
