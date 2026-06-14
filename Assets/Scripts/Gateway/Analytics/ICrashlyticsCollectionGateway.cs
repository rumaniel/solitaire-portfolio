namespace Gateway.Analytics
{
    /// <summary>Controls Firebase Crashlytics auto-collection. Default is off via AndroidManifest meta-data (FirebaseAnalyticsConsent.androidlib); opt-in once user consent is granted.</summary>
    public interface ICrashlyticsCollectionGateway
    {
        void SetCollectionEnabled(bool enabled);
    }
}
