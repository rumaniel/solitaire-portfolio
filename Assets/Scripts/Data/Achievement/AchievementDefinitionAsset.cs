using Model.Achievement;
using Model.Game;
using UnityEngine;
using UnityEngine.Localization;

namespace Data.Achievement
{
    /// <summary>ScriptableObject definition of one achievement; UI-only sprite field lives here.</summary>
    [CreateAssetMenu(fileName = "Achievement", menuName = "Solitaire/Achievement/Definition")]
    public class AchievementDefinitionAsset : ScriptableObject, IAchievementDefinition
    {
        [Header("Identity")]
        [SerializeField] private string id;

        [Header("Localization")]
        [SerializeField] private LocalizedString titleEntry;
        [SerializeField] private LocalizedString descriptionEntry;

        [Header("Icon")]
        [SerializeField] private Sprite icon;

        [Header("Display")]
        [SerializeField] private bool isHidden;
        [SerializeField] private bool isIncremental;

        [Header("Rule")]
        [SerializeField] private AchievementRuleType ruleType;
        [SerializeField] private int targetInt;
        [SerializeField] private float targetFloat;
        [SerializeField] private GameType scopeGameType = GameType.None;

        [Header("Platform Mapping (leave empty to skip a platform)")]
        [SerializeField] private string googlePlayId;
        [SerializeField] private string gameCenterId;
        [SerializeField] private string steamId;

        public string Id => id;
        public string TitleKey => titleEntry.IsEmpty ? string.Empty : (titleEntry.TableEntryReference.Key ?? string.Empty);
        public string DescriptionKey => descriptionEntry.IsEmpty ? string.Empty : (descriptionEntry.TableEntryReference.Key ?? string.Empty);
        public Sprite Icon => icon;
        public LocalizedString TitleEntry => titleEntry;
        public LocalizedString DescriptionEntry => descriptionEntry;
        public bool IsHidden => isHidden;
        public bool IsIncremental => isIncremental;
        public AchievementRuleType RuleType => ruleType;
        public int TargetInt => targetInt;
        public float TargetFloat => targetFloat;
        public GameType ScopeGameType => scopeGameType;
        public string GooglePlayId => googlePlayId ?? string.Empty;
        public string GameCenterId => gameCenterId ?? string.Empty;
        public string SteamId => steamId ?? string.Empty;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (isIncremental && string.IsNullOrWhiteSpace(googlePlayId))
                Debug.LogWarning(
                    $"[Achievement] '{id}' is incremental but has no GooglePlayId — progress will not mirror to Play Games.",
                    this);

            if (!string.IsNullOrEmpty(googlePlayId) && googlePlayId != googlePlayId.Trim())
                Debug.LogWarning(
                    $"[Achievement] '{id}' GooglePlayId has surrounding whitespace ('{googlePlayId}') — trim before publishing.",
                    this);
        }
#endif
    }
}
