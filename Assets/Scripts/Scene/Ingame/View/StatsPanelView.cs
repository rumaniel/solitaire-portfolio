using Model.Stats;
using Shared;
using TMPro;
using UnityEngine;

namespace Scene.Ingame.View
{
    /// <summary>
    /// Full-screen overlay for lifetime statistics.
    ///
    /// Hierarchy (create in Unity Editor):
    ///   StatsPanel (RectTransform: stretch-all, CanvasGroup)
    ///     Background (Image: semi-transparent black)
    ///     Content (RectTransform: centered, scrollable)
    ///       TitleText (TMP) — "Statistics"
    ///
    ///       [General]
    ///       GamesPlayedValue (TMP)
    ///       GamesWonValue (TMP)
    ///       GamesLostValue (TMP)
    ///       WinRateValue (TMP)
    ///
    ///       [Time]
    ///       ShortestWinTimeValue (TMP)
    ///       LongestWinTimeValue (TMP)
    ///       AverageWinTimeValue (TMP)
    ///
    ///       [Moves]
    ///       MinWinMovesValue (TMP)
    ///       MaxWinMovesValue (TMP)
    ///       AverageWinMovesValue (TMP)
    ///
    ///       [Score]
    ///       HighScoreValue (TMP)
    ///       AverageScoreValue (TMP)
    ///
    ///       [Streaks]
    ///       CurrentStreakValue (TMP)
    ///       BestStreakValue (TMP)
    ///
    ///       [Special]
    ///       WonWithoutUndoValue (TMP)
    ///
    ///       CloseButton (Button + TMP child "Close")
    ///
    /// Wire CloseButton.onClick → StatsPanelView.Hide() (or IngameComponent.HideStatsPanel())
    /// </summary>
    public class StatsPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;

        [Header("General")]
        [SerializeField] private TMP_Text gamesPlayedText;
        [SerializeField] private TMP_Text gamesWonText;
        [SerializeField] private TMP_Text gamesLostText;
        [SerializeField] private TMP_Text winRateText;

        [Header("Time")]
        [SerializeField] private TMP_Text shortestWinTimeText;
        [SerializeField] private TMP_Text longestWinTimeText;
        [SerializeField] private TMP_Text averageWinTimeText;

        [Header("Moves")]
        [SerializeField] private TMP_Text minWinMovesText;
        [SerializeField] private TMP_Text maxWinMovesText;
        [SerializeField] private TMP_Text averageWinMovesText;

        [Header("Score")]
        [SerializeField] private TMP_Text highScoreText;
        [SerializeField] private TMP_Text averageScoreText;

        [Header("Streaks")]
        [SerializeField] private TMP_Text currentStreakText;
        [SerializeField] private TMP_Text bestStreakText;

        [Header("Special")]
        [SerializeField] private TMP_Text wonWithoutUndoText;

        public void Show(LifetimeStats stats)
        {
            bool hasWins = stats.TotalGamesWon > 0;

            // General
            gamesPlayedText.SetText("{0}", stats.TotalGamesPlayed);
            gamesWonText.SetText("{0}", stats.TotalGamesWon);
            gamesLostText.SetText("{0}", stats.TotalGamesLost);
            winRateText.text = stats.TotalGamesPlayed > 0
                ? string.Concat((stats.WinRate * 100f).ToString("F1"), "%")
                : "-";

            // Time
            shortestWinTimeText.text = hasWins ? TimeFormatHelper.Format(stats.ShortestWinTime) : "-";
            longestWinTimeText.text = hasWins ? TimeFormatHelper.Format(stats.LongestWinTime) : "-";
            averageWinTimeText.text = hasWins ? TimeFormatHelper.Format(stats.AverageWinTime) : "-";

            // Moves
            minWinMovesText.SetText(hasWins ? "{0}" : "-", hasWins ? stats.MinWinMoves : 0);
            maxWinMovesText.SetText(hasWins ? "{0}" : "-", hasWins ? stats.MaxWinMoves : 0);
            averageWinMovesText.text = hasWins
                ? stats.AverageWinMoves.ToString("F1")
                : "-";

            // Score
            highScoreText.SetText(hasWins ? "{0}" : "-", hasWins ? stats.HighScore : 0);
            averageScoreText.text = stats.TotalGamesPlayed > 0
                ? stats.AverageScore.ToString("F1")
                : "-";

            // Streaks
            currentStreakText.SetText("{0}", stats.CurrentWinStreak);
            bestStreakText.SetText("{0}", stats.BestWinStreak);

            // Special
            wonWithoutUndoText.SetText("{0}", stats.GamesWonWithoutUndo);

            panel.SetActive(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
        }

    }
}
