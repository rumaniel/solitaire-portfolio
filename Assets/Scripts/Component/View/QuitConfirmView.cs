using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Component.View
{
    /// <summary>
    /// Simple quit-confirmation dialog. Attach a <see cref="Component.EventSystem.BackButtonLayer"/>
    /// on the panel child with onBack → Hide() for back-to-cancel behavior.
    /// </summary>
    public class QuitConfirmView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private void Awake()
        {
            confirmButton?.OnClickAsObservable().Subscribe(_ => Quit()).AddTo(this);
            cancelButton?.OnClickAsObservable().Subscribe(_ => Hide()).AddTo(this);
        }

        public void Show()
        {
            if (panel != null) panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
