using Component.Helpers;
using Model.Stats;
using R3;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Scene.Lobby.View
{
    public class DailyTileView : MonoBehaviour
    {
        public enum DailyState
        {
            NotStarted,
            InProgress,
            Completed,
        }

        [Header("References")]
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text streakText;
        [SerializeField] private LocalizeStringEvent streakLocalizer;

        [Header("Status Copy")]
        [SerializeField] private string notStartedLabel = "Play Today's Deal";
        [SerializeField] private string inProgressLabel = "Continue";
        [SerializeField] private string completedLabel = "Completed - Come back tomorrow";

        private readonly Subject<Unit> clickSubject = new();

        public Observable<Unit> OnClickObservable => clickSubject;

        private void Awake()
        {
            button?.OnClickAsObservable().Subscribe(clickSubject.OnNext).AddTo(this);
        }

        private void OnDestroy()
        {
            clickSubject.Dispose();
        }

        public void Apply(DailyState state, string dateLabel, int currentStreak,
            DailyRecord completedRecord = null, float elapsedSeconds = -1f)
        {
            if (dateText != null)
                dateText.text = dateLabel ?? string.Empty;

            if (streakLocalizer != null)
            {
                bool hasStreak = currentStreak > 0;
                streakLocalizer.gameObject.SetActive(hasStreak);
                if (hasStreak) streakLocalizer.SetIntVar("count", currentStreak);
            }
            else if (streakText != null)
            {
                // Fallback: English-only when localizer is unwired.
                bool hasStreak = currentStreak > 0;
                streakText.gameObject.SetActive(hasStreak);
                if (hasStreak) streakText.SetText("Streak: {0} day", currentStreak);
            }

            if (statusText != null)
            {
                statusText.text = state switch
                {
                    DailyState.NotStarted => notStartedLabel,
                    DailyState.InProgress => elapsedSeconds >= 0f
                        ? $"{inProgressLabel} · {TimeFormatHelper.Format(elapsedSeconds)}"
                        : inProgressLabel,
                    DailyState.Completed when completedRecord != null =>
                        $"{completedLabel}\nScore {completedRecord.Score} · {TimeFormatHelper.Format(completedRecord.ElapsedSeconds)} · {completedRecord.MoveCount} moves",
                    DailyState.Completed => completedLabel,
                    _ => string.Empty,
                };
            }

            if (button != null)
                button.interactable = true;
        }
    }
}
