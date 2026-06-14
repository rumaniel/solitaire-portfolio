using System;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.Ingame.View
{
    /// <summary>
    /// Full-screen overlay shown when no valid moves remain (stuck/lost).
    /// Offers "Undo", "Restart", and "New Game" actions.
    /// Also doubles as auto-complete prompt when triggered.
    ///
    /// Hierarchy (create in Unity Editor):
    ///   StuckPanel (RectTransform: stretch-all)
    ///     Background (Image: semi-transparent black)
    ///     Content (RectTransform: centered)
    ///       MessageText (TMP) — "No Moves Available"
    ///       UndoButton (Button)
    ///       RestartButton (Button)  — restart same deal
    ///       NewGameButton (Button)
    ///       AutoCompleteButton (Button, hidden by default)
    /// </summary>
    public class StuckPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject stuckContent;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private GameObject autoCompletePanel;
        [SerializeField] private Button autoCompleteButton;

        private readonly Subject<Unit> onNewGameSubject = new();
        private readonly Subject<Unit> onRestartSubject = new();
        private readonly Subject<Unit> onUndoSubject = new();
        private bool lastCanUndo;

        // Re-targeted each time ShowAutoCompletePrompt is called; a single
        // OnClickAsObservable subscription in Awake dispatches to whichever
        // observer is currently active.
        private IObserver<Unit> activeAutoCompleteTarget;

        public Observable<Unit> OnNewGameObservable => onNewGameSubject;
        public Observable<Unit> OnRestartObservable => onRestartSubject;
        public Observable<Unit> OnUndoObservable => onUndoSubject;

        private void Awake()
        {
            newGameButton?.OnClickAsObservable().Subscribe(onNewGameSubject.OnNext).AddTo(this);
            restartButton?.OnClickAsObservable().Subscribe(onRestartSubject.OnNext).AddTo(this);
            undoButton?.OnClickAsObservable().Subscribe(onUndoSubject.OnNext).AddTo(this);
            autoCompleteButton?.OnClickAsObservable()
                .Subscribe(_ => activeAutoCompleteTarget?.OnNext(Unit.Default))
                .AddTo(this);
        }

        public void Show(bool canUndo = true)
        {
            lastCanUndo = canUndo;
            activeAutoCompleteTarget = null;
            if (stuckContent != null) stuckContent.SetActive(true);
            if (autoCompletePanel != null) autoCompletePanel.SetActive(false);
            if (undoButton != null) undoButton.interactable = canUndo;
            if (panel != null) panel.SetActive(true);
        }

        public void Hide()
        {
            activeAutoCompleteTarget = null;
            if (autoCompletePanel != null) autoCompletePanel.SetActive(false);
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>
        /// Invoked by BackButtonLayer. Undo if available, else close.
        /// When undo fires, Presenter handles hiding via OnUndoObservable subscription.
        /// </summary>
        public void TriggerBack()
        {
            if (lastCanUndo)
                onUndoSubject.OnNext(Unit.Default);
            else
                Hide();
        }

        public void ShowAutoCompletePrompt(IObserver<Unit> autoCompleteObserver)
        {
            activeAutoCompleteTarget = autoCompleteObserver;
            if (stuckContent != null) stuckContent.SetActive(false);
            if (panel != null) panel.SetActive(true);
            if (autoCompletePanel != null) autoCompletePanel.SetActive(true);
        }

        public void HideAutoCompletePrompt()
        {
            activeAutoCompleteTarget = null;
            if (autoCompletePanel != null) autoCompletePanel.SetActive(false);
        }

        private void OnDestroy()
        {
            onNewGameSubject?.Dispose();
            onRestartSubject?.Dispose();
            onUndoSubject?.Dispose();
        }
    }
}
