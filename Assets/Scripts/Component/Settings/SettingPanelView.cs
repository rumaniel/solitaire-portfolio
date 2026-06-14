using System;
using System.Collections.Generic;
using Component.Skin;
using Core;
using Cysharp.Threading.Tasks;
using Data.Audio;
using Model.App;
using R3;
using Service.AudioService;
using Service.HapticService;
using Service.LayoutService;
using Service.LocalizationService;
using Service.SkinService;
using Service.UserService;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using VContainer;

namespace Component.Settings
{
    /// <summary>Shared Settings panel. Self-contained — reads services directly, no presenter.</summary>
    public class SettingPanelView : ComponentBase
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Toggle sfxToggle;
        [SerializeField] private Toggle hapticToggle;
        [SerializeField] private Toggle leftHandedToggle;
        [SerializeField] private Button privacyButton;
        [SerializeField] private Button rateButton;
        [SerializeField] private Button licensesButton;
        [SerializeField] private Button copyUserIdButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text versionText;
        [SerializeField] private TMP_Text userIdText;
        [SerializeField] private LicensesPanelView licensesPanelView;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private SkinSelectView skinSelectView;

        [Inject] private IAudioService AudioService { get; set; }
        [Inject] private IHapticService HapticService { get; set; }
        [Inject] private ILayoutService LayoutService { get; set; }
        [Inject] private IUserService UserService { get; set; }
        [Inject] private IAppConfig AppConfig { get; set; }
        [Inject] private ILocalizationService LocalizationService { get; set; }
        [Inject] private ISkinService SkinService { get; set; }

        private IReadOnlyList<Locale> dropdownLocales;

        private readonly CompositeDisposable disposable = new();

        protected override void Awake()
        {
            base.Awake();

            musicToggle?.OnValueChangedAsObservable().Subscribe(OnMusicToggled).AddTo(this);
            sfxToggle?.OnValueChangedAsObservable().Subscribe(OnSfxToggled).AddTo(this);
            hapticToggle?.OnValueChangedAsObservable().Subscribe(OnHapticToggled).AddTo(this);
            leftHandedToggle?.OnValueChangedAsObservable().Subscribe(OnLeftHandedToggled).AddTo(this);
            privacyButton?.OnClickAsObservable().Subscribe(_ => OnPrivacyClicked()).AddTo(this);
            rateButton?.OnClickAsObservable().Subscribe(_ => OnRateClicked()).AddTo(this);
            licensesButton?.OnClickAsObservable().Subscribe(_ => OnLicensesClicked()).AddTo(this);
            copyUserIdButton?.OnClickAsObservable().Subscribe(_ => OnCopyUserIdClicked()).AddTo(this);
            closeButton?.OnClickAsObservable().Subscribe(_ => Hide()).AddTo(this);

            if (languageDropdown != null)
            {
                languageDropdown.onValueChanged
                    .AsObservable<int>()
                    .Subscribe(OnLanguageDropdownChanged)
                    .AddTo(disposable);
            }
            // Re-sync the dropdown when locale changes from elsewhere.
            if (LocalizationService != null && languageDropdown != null)
            {
                LocalizationService.OnLocaleChanged
                    .Subscribe(_ => SyncDropdownSelection())
                    .AddTo(disposable);
            }

            // Skin selection — route taps to the service, mirror current selection in the grid.
            if (skinSelectView != null)
            {
                skinSelectView.OnSkinSelectedObservable()
                    .Subscribe(id =>
                    {
                        AudioService?.Play(AudioCatalog.UI.Click);
                        SkinService?.SelectSkinAsync(id).Forget();
                    })
                    .AddTo(disposable);
            }
            if (SkinService != null && skinSelectView != null)
            {
                SkinService.CurrentSkinId
                    .Subscribe(id => skinSelectView.SetSelected(id))
                    .AddTo(disposable);
            }

            if (UserService != null)
            {
                UserService.User
                    .AsObservable()
                    .Subscribe(RefreshUserId)
                    .AddTo(disposable);
            }

            if (versionText != null)
                versionText.text = $"v{Application.version}";

            if (rateButton != null && string.IsNullOrEmpty(BuildRateUrl()))
                rateButton.interactable = false;
            if (privacyButton != null && string.IsNullOrEmpty(AppConfig?.PrivacyPolicyUrl))
                privacyButton.interactable = false;
        }

        public void Show()
        {
            if (panel == null) return;

            if (musicToggle != null && AudioService != null)
                musicToggle.SetIsOnWithoutNotify(!AudioService.IsMusicMuted);
            if (sfxToggle != null && AudioService != null)
                sfxToggle.SetIsOnWithoutNotify(!AudioService.IsSfxMuted);
            if (hapticToggle != null && HapticService != null)
                hapticToggle.SetIsOnWithoutNotify(HapticService.IsEnabled);
            if (leftHandedToggle != null && LayoutService != null)
                leftHandedToggle.SetIsOnWithoutNotify(LayoutService.IsLeftHanded);

            PopulateLanguageDropdown();

            if (skinSelectView != null && SkinService != null)
            {
                skinSelectView.Build(SkinService.AvailableSkins);
                skinSelectView.SetSelected(SkinService.CurrentSkinId.CurrentValue);
            }

            licensesPanelView?.Hide();
            panel.SetActive(true);
        }

        private void PopulateLanguageDropdown()
        {
            if (languageDropdown == null || LocalizationService == null) return;
            dropdownLocales = LocalizationService.AvailableLocales;
            if (dropdownLocales == null || dropdownLocales.Count == 0) return;

            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var locale in dropdownLocales)
            {
                var label = string.IsNullOrEmpty(locale.LocaleName)
                    ? locale.Identifier.Code
                    : locale.LocaleName;
                options.Add(new TMP_Dropdown.OptionData(label));
            }
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(options);
            SyncDropdownSelection();
        }

        private void SyncDropdownSelection()
        {
            if (languageDropdown == null || dropdownLocales == null || LocalizationService == null) return;
            var current = LocalizationService.CurrentLocale;
            if (current == null) return;
            for (int i = 0; i < dropdownLocales.Count; i++)
            {
                // Compare by Identifier — reloaded Locale assets may differ by instance.
                if (dropdownLocales[i] != null
                    && dropdownLocales[i].Identifier == current.Identifier)
                {
                    languageDropdown.SetValueWithoutNotify(i);
                    break;
                }
            }
        }

        private void OnLanguageDropdownChanged(int index)
        {
            if (dropdownLocales == null || index < 0 || index >= dropdownLocales.Count) return;
            var locale = dropdownLocales[index];
            if (locale == null) return;
            var current = LocalizationService?.CurrentLocale;
            if (current != null && locale.Identifier == current.Identifier) return;
            LocalizationService?.SetLocaleAsync(locale).Forget();
        }

        public void Hide()
        {
            licensesPanelView?.Hide();
            if (panel != null) panel.SetActive(false);
        }

        public void TriggerBack() => Hide();

        private void OnMusicToggled(bool isOn) => AudioService?.SetMusicMuted(!isOn);
        private void OnSfxToggled(bool isOn) => AudioService?.SetSfxMuted(!isOn);

        private void OnHapticToggled(bool isOn)
        {
            HapticService?.SetEnabled(isOn);
            if (isOn) HapticService?.Trigger(HapticTier.Light);
        }

        private void OnLeftHandedToggled(bool isOn) => LayoutService?.SetLeftHanded(isOn);

        private void OnPrivacyClicked()
        {
            var url = AppConfig?.PrivacyPolicyUrl;
            if (!string.IsNullOrEmpty(url))
                Application.OpenURL(url);
        }

        private void OnRateClicked()
        {
            var url = BuildRateUrl();
            if (!string.IsNullOrEmpty(url))
                Application.OpenURL(url);
        }

        private void OnLicensesClicked() => licensesPanelView?.Show();

        private void OnCopyUserIdClicked()
        {
            var id = UserService?.User?.Value?.UserId;
            if (!string.IsNullOrEmpty(id))
                GUIUtility.systemCopyBuffer = id;
        }

        private void RefreshUserId(Model.User.User user)
        {
            if (userIdText == null) return;
            userIdText.text = string.IsNullOrEmpty(user?.UserId) ? "-" : user.UserId;
        }

        private string BuildRateUrl()
        {
            var ios = AppConfig?.IosStoreId;
            var android = AppConfig?.AndroidStoreId;

            switch (Application.platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    if (string.IsNullOrEmpty(ios)) return null;
                    return IsHttpUrl(ios) ? ios : $"itms-apps://itunes.apple.com/app/id{ios}";
                case RuntimePlatform.Android:
                    if (string.IsNullOrEmpty(android)) return null;
                    return IsHttpUrl(android) ? android : $"market://details?id={android}";
                default:
                    if (!string.IsNullOrEmpty(android))
                        return IsHttpUrl(android) ? android : $"https://play.google.com/store/apps/details?id={android}";
                    if (!string.IsNullOrEmpty(ios))
                        return IsHttpUrl(ios) ? ios : $"https://apps.apple.com/app/id{ios}";
                    return null;
            }
        }

        private static bool IsHttpUrl(string s)
            => !string.IsNullOrEmpty(s)
               && (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                   || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

        private void OnDestroy()
        {
            disposable.Dispose();
        }
    }
}
