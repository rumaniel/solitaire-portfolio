using Model.Achievement;
using Model.Game;

namespace Tests.EditMode
{
    /// <summary>Programmatic <see cref="IAchievementDefinition"/> for tests — bypasses ScriptableObject.</summary>
    public class StubAchievementDefinition : IAchievementDefinition
    {
        public string Id { get; set; } = "test";
        public string TitleKey { get; set; } = "Test";
        public string DescriptionKey { get; set; } = "";
        public bool IsHidden { get; set; }
        public bool IsIncremental { get; set; }
        public AchievementRuleType RuleType { get; set; }
        public int TargetInt { get; set; }
        public float TargetFloat { get; set; }
        public GameType ScopeGameType { get; set; } = GameType.Klondike;
        public string GooglePlayId { get; set; } = string.Empty;
        public string GameCenterId { get; set; } = string.Empty;
        public string SteamId { get; set; } = string.Empty;
    }
}
