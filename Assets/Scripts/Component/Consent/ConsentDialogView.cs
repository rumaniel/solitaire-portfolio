using System.Threading;
using Core;
using Cysharp.Threading.Tasks;
using Model.App;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Component.Consent
{
    /// <summary>첫 실행 동의 모달. 3 체크박스 모두 ON일 때만 Accept 활성. ShowAsync로 await 가능.</summary>
    public class ConsentDialogView : ComponentBase
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Toggle tosToggle;
        [SerializeField] private Toggle privacyToggle;
        [SerializeField] private Toggle ageToggle;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;
        [SerializeField] private Button viewPrivacyButton;

        [Inject] private IAppConfig AppConfig { get; set; }

        private UniTaskCompletionSource<bool> completion;

        protected override void Awake()
        {
            base.Awake();
            // ComponentBase의 auto-inject는 LifetimeScope.Find<SceneBase>(scene) 경로라
            // App scene(SceneBase 아님)에서는 silent fail. [Inject] 필드는
            // AppLifetimeScope의 RegisterComponent 후 build-phase에서 채워지므로
            // 이 Awake 본문에서 [Inject] 필드(AppConfig 등)를 직접 사용하면 NRE가 난다.

            tosToggle?.OnValueChangedAsObservable().Subscribe(_ => RefreshAccept()).AddTo(this);
            privacyToggle?.OnValueChangedAsObservable().Subscribe(_ => RefreshAccept()).AddTo(this);
            ageToggle?.OnValueChangedAsObservable().Subscribe(_ => RefreshAccept()).AddTo(this);

            acceptButton?.OnClickAsObservable().Subscribe(_ => OnAccept()).AddTo(this);
            declineButton?.OnClickAsObservable().Subscribe(_ => OnDecline()).AddTo(this);
            viewPrivacyButton?.OnClickAsObservable().Subscribe(_ => OnViewPrivacy()).AddTo(this);

            // Localization 초기화 전에 placeholder 텍스트가 보이지 않도록 강제 비활성.
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>다이얼로그 표시 후 Accept(true) 또는 Decline(false)까지 대기.</summary>
        public UniTask<bool> ShowAsync(CancellationToken ct = default)
        {
            tosToggle?.SetIsOnWithoutNotify(false);
            privacyToggle?.SetIsOnWithoutNotify(false);
            ageToggle?.SetIsOnWithoutNotify(false);
            RefreshAccept();

            if (panel != null) panel.SetActive(true);
            completion = new UniTaskCompletionSource<bool>();
            ct.Register(() => completion?.TrySetCanceled());
            return completion.Task;
        }

        private void RefreshAccept()
        {
            if (acceptButton == null) return;
            var allChecked = (tosToggle != null && tosToggle.isOn)
                && (privacyToggle != null && privacyToggle.isOn)
                && (ageToggle != null && ageToggle.isOn);
            acceptButton.interactable = allChecked;
        }

        private void OnAccept()
        {
            if (panel != null) panel.SetActive(false);
            completion?.TrySetResult(true);
        }

        private void OnDecline()
        {
            if (panel != null) panel.SetActive(false);
            completion?.TrySetResult(false);
        }

        private void OnViewPrivacy()
        {
            var url = AppConfig?.PrivacyPolicyUrl;
            if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
        }
    }
}
