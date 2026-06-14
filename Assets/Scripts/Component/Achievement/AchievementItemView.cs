using System;
using Data.Achievement;
using Model.Achievement;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Component.Achievement
{
    /// <summary>Single achievement row. Hidden-locked entries render "???" placeholders to avoid spoilers.</summary>
    public class AchievementItemView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [Required, SerializeField] private LocalizeStringEvent titleLocalizer;
        [Required, SerializeField] private LocalizeStringEvent descriptionLocalizer;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Slider progressBar;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private GameObject unlockedBadge;

        // Shared to avoid allocating on every Render.
        private static readonly LocalizedString HiddenPlaceholder = new("UI", "ui.hidden_placeholder");

        private void Awake()
        {
            // Runtime fail-loud for prefabs where Inspector bindings were missed.
            if (titleLocalizer == null)
                Debug.LogError($"[AchievementItemView] {name}: titleLocalizer is not wired.", this);
            if (descriptionLocalizer == null)
                Debug.LogError($"[AchievementItemView] {name}: descriptionLocalizer is not wired.", this);
        }

        public void Render(IAchievementDefinition definition, AchievementStatus status)
        {
            bool unlocked = status.State == AchievementState.Unlocked;
            bool concealed = definition.IsHidden && !unlocked;

            var defAsset = definition as AchievementDefinitionAsset;
            if (iconImage != null)
            {
                var sprite = defAsset?.Icon;
                iconImage.sprite = sprite;
                iconImage.enabled = sprite != null;
                iconImage.color = Color.white;
            }

            if (titleLocalizer != null)
            {
                titleLocalizer.StringReference = concealed
                    ? HiddenPlaceholder
                    : (defAsset?.TitleEntry ?? HiddenPlaceholder);
                titleLocalizer.RefreshString();
            }
            if (descriptionLocalizer != null)
            {
                descriptionLocalizer.StringReference = concealed
                    ? HiddenPlaceholder
                    : (defAsset?.DescriptionEntry ?? HiddenPlaceholder);
                descriptionLocalizer.RefreshString();
            }

            bool showProgressBar = definition.IsIncremental && !concealed;
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(showProgressBar);
                if (showProgressBar && definition.TargetInt > 0)
                {
                    progressBar.minValue = 0;
                    progressBar.maxValue = definition.TargetInt;
                    progressBar.value = status.CurrentProgress;
                }
            }

            if (progressText != null)
            {
                if (concealed)
                    progressText.text = string.Empty;
                else if (definition.IsIncremental)
                    progressText.text = $"{status.CurrentProgress}/{definition.TargetInt}";
                else if (unlocked)
                    progressText.text = FormatUnlockedDate(status.UnlockedAtUnix);
                else
                    progressText.text = string.Empty;
            }

            if (lockedOverlay != null)
                lockedOverlay.SetActive(!unlocked);
            if (unlockedBadge != null)
                unlockedBadge.SetActive(unlocked);
        }

        private static string FormatUnlockedDate(long unix)
        {
            if (unix <= 0) return string.Empty;
            var date = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().DateTime;
            return $"Unlocked {date:yyyy-MM-dd}";
        }
    }
}
