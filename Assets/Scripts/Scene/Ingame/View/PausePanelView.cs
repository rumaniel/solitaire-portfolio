using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.Ingame.View
{
    /// <summary>Pause overlay exposing resume, new-game, and restart events.</summary>
    public class PausePanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button toGameButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button playWithCodeButton;
        [SerializeField] private Button lobbyButton;

        private readonly Subject<Unit> onToGameSubject = new();
        private readonly Subject<Unit> onNewGameSubject = new();
        private readonly Subject<Unit> onRestartSubject = new();
        private readonly Subject<Unit> onPlayWithCodeSubject = new();
        private readonly Subject<Unit> onLobbySubject = new();

        public Observable<Unit> OnToGameObservable => onToGameSubject;
        public Observable<Unit> OnNewGameObservable => onNewGameSubject;
        public Observable<Unit> OnRestartObservable => onRestartSubject;
        public Observable<Unit> OnPlayWithCodeObservable => onPlayWithCodeSubject;
        public Observable<Unit> OnLobbyObservable => onLobbySubject;

        private void Awake()
        {
            toGameButton?.OnClickAsObservable().Subscribe(onToGameSubject.OnNext).AddTo(this);
            newGameButton?.OnClickAsObservable().Subscribe(onNewGameSubject.OnNext).AddTo(this);
            restartButton?.OnClickAsObservable().Subscribe(onRestartSubject.OnNext).AddTo(this);
            playWithCodeButton?.OnClickAsObservable().Subscribe(onPlayWithCodeSubject.OnNext).AddTo(this);
            lobbyButton?.OnClickAsObservable().Subscribe(onLobbySubject.OnNext).AddTo(this);
        }

        /// <summary>Invoked by BackButtonLayer to resume the game.</summary>
        public void TriggerResume() => onToGameSubject.OnNext(Unit.Default);

        public void Show()
        {
            if (panel != null) panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnDestroy()
        {
            onToGameSubject.Dispose();
            onNewGameSubject.Dispose();
            onRestartSubject.Dispose();
            onPlayWithCodeSubject.Dispose();
            onLobbySubject.Dispose();
        }
    }
}
