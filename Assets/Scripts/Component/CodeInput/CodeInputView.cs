using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Component.CodeInput
{
    /// <summary>Panel for entering a shared game code to replay a specific deal.</summary>
    public class CodeInputView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private Button playButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text errorText;

        private readonly Subject<string> onPlayWithCodeSubject = new();

        /// <summary>Emitted with the entered code when the user taps Play.</summary>
        public Observable<string> OnPlayWithCodeObservable => onPlayWithCodeSubject;

        private void Awake()
        {
            playButton?.OnClickAsObservable().Subscribe(_ => OnPlayClicked()).AddTo(this);
            closeButton?.OnClickAsObservable().Subscribe(_ => Hide()).AddTo(this);
        }

        public void Show() => Show(string.Empty);

        public void Show(string prefill)
        {
            if (codeInput != null)
                codeInput.text = prefill ?? string.Empty;
            if (errorText != null)
                errorText.text = string.Empty;
            if (panel != null)
                panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null)
                panel.SetActive(false);
        }

        public void ShowError(string message)
        {
            if (errorText != null)
                errorText.text = message;
        }

        private void OnPlayClicked()
        {
            if (codeInput == null) return;
            var code = codeInput.text;
            if (string.IsNullOrWhiteSpace(code)) return;
            onPlayWithCodeSubject.OnNext(code.Trim());
        }

        private void OnDestroy()
        {
            onPlayWithCodeSubject?.Dispose();
        }
    }
}
