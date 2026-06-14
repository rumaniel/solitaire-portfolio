using Model.Game;

namespace Model.Achievement
{
    /// <summary>
    /// Immutable metadata for one achievement. UnityEngine-specific fields (icon) live on the Data-layer
    /// ScriptableObject implementation, parallel to <see cref="Model.Stats.IScoreRule"/>.
    /// </summary>
    public interface IAchievementDefinition
    {
        string Id { get; }
        /// <summary>StringTable entry key (e.g. "achievement.klondike.first_win.title"). Resolved via <c>ILocalizationService</c>.</summary>
        string TitleKey { get; }
        /// <summary>StringTable entry key for the description. See <see cref="TitleKey"/>.</summary>
        string DescriptionKey { get; }
        bool IsHidden { get; }
        bool IsIncremental { get; }
        AchievementRuleType RuleType { get; }
        int TargetInt { get; }
        float TargetFloat { get; }
        GameType ScopeGameType { get; }

        /// <summary>Google Play Games achievement ID (from Play Console). Empty means "do not mirror to GPGS".</summary>
        string GooglePlayId { get; }
        /// <summary>Apple Game Center achievement ID. Reserved for future use.</summary>
        string GameCenterId { get; }
        /// <summary>Steam achievement API name. Reserved for future use.</summary>
        string SteamId { get; }
    }
}
