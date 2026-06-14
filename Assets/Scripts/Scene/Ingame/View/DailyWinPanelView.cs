using Component.Helpers;
using R3;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Scene.Ingame.View
{
    /// <summary>
    /// Daily-specific victory overlay. Shows result stats + streak + pre-rendered
    /// share text preview. Copy/Twitter/Lobby buttons are fixed — add more by
    /// extending this view (no runtime button factory).
    /// </summary>
    public class DailyWinPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private TMP_Text scoreValueText;
        [SerializeField] private TMP_Text timeValueText;
        [SerializeField] private TMP_Text moveValueText;
        [SerializeField] private TMP_Text streakValueText;
        [SerializeField] private LocalizeStringEvent streakLocalizer;
        [SerializeField] private Text sharePreviewText;
        [SerializeField] private Button copyButton;
        [SerializeField] private Button twitterButton;
        [SerializeField] private Button lobbyButton;

        private readonly Subject<Unit> onCopySubject = new();
        private readonly Subject<Unit> onTwitterSubject = new();
        private readonly Subject<Unit> onLobbySubject = new();

        public Observable<Unit> OnCopyObservable => onCopySubject;
        public Observable<Unit> OnTwitterObservable => onTwitterSubject;
        public Observable<Unit> OnLobbyObservable => onLobbySubject;

        /// <summary>Last rendered share text — Presenter reads this when handling copy/twitter.</summary>
        public string ShareText { get; private set; }

        private void Awake()
        {
            copyButton?.OnClickAsObservable().Subscribe(onCopySubject.OnNext).AddTo(this);
            twitterButton?.OnClickAsObservable().Subscribe(onTwitterSubject.OnNext).AddTo(this);
            lobbyButton?.OnClickAsObservable().Subscribe(onLobbySubject.OnNext).AddTo(this);
        }

        public void Show(int score, int moves, float elapsedSeconds, int streak, string shareText, string dateLabel)
        {
            ShareText = shareText;
            if (dateText != null) dateText.text = dateLabel ?? string.Empty;
            if (scoreValueText != null) scoreValueText.SetText("{0}", score);
            if (moveValueText != null) moveValueText.SetText("{0}", moves);
            if (timeValueText != null) timeValueText.text = TimeFormatHelper.Format(elapsedSeconds);
            if (streakLocalizer != null)
            {
                streakLocalizer.SetIntVar("count", streak);
            }
            else if (streakValueText != null)
            {
                // Fallback: English-only, no plural.
                streakValueText.SetText("Streak: {0} day", streak);
            }
            if (sharePreviewText != null) sharePreviewText.text = shareText ?? string.Empty;
            if (panel != null) panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnDestroy()
        {
            onCopySubject?.Dispose();
            onTwitterSubject?.Dispose();
            onLobbySubject?.Dispose();
        }
    }
}
