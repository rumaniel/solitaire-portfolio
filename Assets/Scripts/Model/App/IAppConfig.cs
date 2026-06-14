namespace Model.App
{
    /// <summary>
    /// Read-only view over app-wide configuration. Lives in Model so that
    /// Service-layer code can depend on it without pulling in Data (the actual
    /// ScriptableObject implementation).
    /// </summary>
    public interface IAppConfig
    {
        /// <summary>Daily share text — appended via the {url} token.</summary>
        string DailyPlayUrl { get; }

        /// <summary>Challenge share text — appended via the {url} token.</summary>
        string ChallengePlayUrl { get; }

        /// <summary>Max number of <c>DailyRecord</c> entries to keep on disk.</summary>
        int DailyStatsHistoryLimit { get; }

        /// <summary>Privacy policy URL opened from the Settings panel. Empty disables the button.</summary>
        string PrivacyPolicyUrl { get; }

        /// <summary>Android package/store id used to build the Play Store deep link. Empty disables the button.</summary>
        string AndroidStoreId { get; }

        /// <summary>iOS App Store numeric id used to build the iTunes deep link. Empty disables the button.</summary>
        string IosStoreId { get; }
    }
}
