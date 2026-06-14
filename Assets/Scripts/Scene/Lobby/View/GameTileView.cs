using Data.Game;
using Model.Game;
using R3;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.Lobby.View
{
    /// <summary>A single game-mode tile in the Lobby grid.</summary>
    public class GameTileView : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("GameVariant asset describing this tile. Leave null for decorative coming-soon tiles.")]
        [SerializeField] private GameVariant variant;

        [Header("References")]
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text titleText;

        [Header("Continue Badge")]
        [SerializeField] private CanvasGroup continueBadge;
        [SerializeField] private TMP_Text continueBadgeText;

        [Header("Coming Soon")]
        [SerializeField] private CanvasGroup comingSoonOverlay;
        [SerializeField] private bool comingSoon;

        private readonly Subject<Unit> clickSubject = new();

        public GameVariant Variant => variant;
        public GameType GameType => variant != null ? variant.GameType : GameType.None;
        public int VariantId => variant != null ? variant.VariantId : 0;
        public bool IsComingSoon => comingSoon;
        public Observable<Unit> OnClickObservable => clickSubject;

        private void Awake()
        {
            button?.OnClickAsObservable().Subscribe(clickSubject.OnNext).AddTo(this);

            ApplyComingSoonState();
            ApplyVariantTitle();

            if (continueBadge != null)
                continueBadge.alpha = 0f;
        }

        private void OnDestroy()
        {
            clickSubject.Dispose();
        }

        public void SetTitle(string title)
        {
            if (titleText != null)
                titleText.SetText(title);
        }

        /// <summary>Reveals the continue badge with elapsed time.</summary>
        public void ShowContinueBadge(float elapsedSeconds)
        {
            if (continueBadge == null)
                return;

            continueBadge.alpha = 1f;
            if (continueBadgeText != null)
            {
                var time = TimeFormatHelper.Format(elapsedSeconds);
                continueBadgeText.SetText($"Continue · {time}");
            }
        }

        public void HideContinueBadge()
        {
            if (continueBadge != null)
                continueBadge.alpha = 0f;
        }

        private void ApplyComingSoonState()
        {
            if (comingSoonOverlay != null)
                comingSoonOverlay.alpha = comingSoon ? 1f : 0f;

            if (button != null)
                button.interactable = !comingSoon;
        }

        private void ApplyVariantTitle()
        {
            if (variant == null || titleText == null) return;
            if (string.IsNullOrEmpty(titleText.text))
                titleText.SetText(variant.DisplayName);
        }
    }
}
