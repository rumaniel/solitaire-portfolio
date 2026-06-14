using R3;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.Ingame.View
{
    /// <summary>Full-screen overlay shown on game win with stats, game code, and action buttons.</summary>
    public class WinPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text scoreValueText;
        [SerializeField] private TMP_Text timeValueText;
        [SerializeField] private TMP_Text moveValueText;
        [SerializeField] private TMP_Text gameCodeText;
        [SerializeField] private Button shareButton;
        [SerializeField] private Button gameCodeButton;
        [SerializeField] private Button lobbyButton;

        private readonly Subject<Unit> onShareSubject = new();
        private readonly Subject<string> onCopyCodeSubject = new();
        private readonly Subject<Unit> onLobbySubject = new();

        public Observable<Unit> OnShareObservable => onShareSubject;
        public Observable<string> OnCopyCodeObservable => onCopyCodeSubject;
        public Observable<Unit> OnLobbyObservable => onLobbySubject;

        private string currentGameCode;

        private void Awake()
        {
            shareButton?.OnClickAsObservable().Subscribe(onShareSubject.OnNext).AddTo(this);
            gameCodeButton?.OnClickAsObservable()
                .Subscribe(_ => onCopyCodeSubject.OnNext(currentGameCode))
                .AddTo(this);
            lobbyButton?.OnClickAsObservable().Subscribe(onLobbySubject.OnNext).AddTo(this);
        }

        /// <summary>Invoked by BackButtonLayer to return to lobby.</summary>
        public void TriggerLobby() => onLobbySubject.OnNext(Unit.Default);

        public void Show(int score, int moves, float elapsedSeconds, string gameCode)
        {
            currentGameCode = gameCode;
            scoreValueText.SetText("{0}", score);
            moveValueText.SetText("{0}", moves);
            timeValueText.text = TimeFormatHelper.Format(elapsedSeconds);

            if (gameCodeText != null)
                gameCodeText.text = gameCode;

            panel.SetActive(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
        }

        private void OnDestroy()
        {
            onShareSubject?.Dispose();
            onCopyCodeSubject?.Dispose();
            onLobbySubject?.Dispose();
        }
    }
}
