using System;
using System.Collections.Generic;
using System.Linq;
using Component.Consent;
using Cysharp.Threading.Tasks;
using Gateway.Analytics;
using Model.User;
using R3;
using Service.AchievementService;
using Service.ConsentService;
using Service.LocalizationService;
using Service.RouteService;
using Service.StatsService;
using Service.UserService;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using VContainer;
using VContainer.Unity;

namespace Scene.Login
{
    public class LoginPresenter : IStartable, IInitializable
    {
        [Inject] private LoginComponent Component { get; set; }
        [Inject] private IUserService UserService { get; set; }
        [Inject] private IRouteService RouteService { get; set; }
        [Inject] private IConsentService ConsentService { get; set; }
        [Inject] private ConsentDialogView ConsentDialog { get; set; }
        [Inject] private IAnalyticsCollectionGateway AnalyticsCollection { get; set; }
        [Inject] private ICrashlyticsCollectionGateway CrashlyticsCollection { get; set; }
        [Inject] private ILocalizationService LocalizationService { get; set; }
        [Inject] private ILifetimeStatsService LifetimeStatsService { get; set; }
        [Inject] private IAchievementService AchievementService { get; set; }
        [Inject] private IPlatformAchievementService PlatformAchievementService { get; set; }
        [Inject] private PlatformAchievementMirror AchievementMirror { get; set; }

        private IReadOnlyList<Locale> dropdownLocales;

        public void Initialize()
        {
            UserService.User.AsObservable().Subscribe(OnUserUpdate).AddTo(Component);
            Component.OnStartClicked.AsObservable().Subscribe(OnClickLogin).AddTo(Component);

            // Login 화면에 언어 드롭다운 — consent 다이얼로그가 떠있는 동안에도 언어 변경 가능.
            // ConsentDialog의 LocalizeStringEvent들이 OnLocaleChanged를 받아 자동으로 갱신된다.
            PopulateLanguageDropdown();
            if (Component.LanguageDropdown != null)
            {
                Component.LanguageDropdown.onValueChanged
                    .AsObservable<int>()
                    .Subscribe(OnLanguageDropdownChanged)
                    .AddTo(Component);
            }
            if (LocalizationService != null)
            {
                LocalizationService.OnLocaleChanged
                    .Subscribe(_ => SyncDropdownSelection())
                    .AddTo(Component);
            }
        }

        public async void Start()
        {
            // 1) Consent gate — 모든 사용자 데이터/계정 연동(Firebase Auth, GPGS, Stats 등)이
            //    실행되기 전에 차단. Localization은 App에서 await되어 다이얼로그 텍스트는 정상.
            if (ConsentService.NeedsConsent)
            {
                var accepted = await ConsentDialog.ShowAsync();
                if (!accepted)
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    return;
                }
                ConsentService.MarkAccepted();
            }

            // 2) Firebase Analytics + Crashlytics 자동 수집 옵트인 — manifest 기본 비활성 →
            //    동의 후 활성화. 재실행 흐름에서도 매 부팅마다 호출(SDK에 idempotent).
            AnalyticsCollection.SetCollectionEnabled(true);
            CrashlyticsCollection.SetCollectionEnabled(true);

            // 3) 사용자 데이터 / 계정 연동 init — Mirror가 AchievementService init보다 먼저
            //    구독해야 retroactive sweep을 잡을 수 있다.
            await LifetimeStatsService.InitializeAsync();
            AchievementMirror.AttachSubscriptions();
            await AchievementService.InitializeAsync();
            await PlatformAchievementService.InitializeAsync();

            // 4) Auth → Lobby
            try
            {
                await UserService.Login();
                await RouteService.NavigateAsync("Lobby", useBlocker: false);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Login] Auto-login failed: {e.Message}");
            }
        }

        private async void OnClickLogin(Unit unit)
        {
            try
            {
                await UserService.Login();
                await RouteService.NavigateAsync("Lobby", useBlocker: false);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Login] Login failed: {e.Message}");
            }
        }

        private void OnUserUpdate(User user)
        {
            Component.UserId.Value = user.UserId;
        }

        private void PopulateLanguageDropdown()
        {
            var dd = Component.LanguageDropdown;
            if (dd == null || LocalizationService == null) return;
            // Pseudo는 Editor 전용 디버그 로케일 — 실제 사용자에게 노출하지 않는다.
            dropdownLocales = LocalizationService.AvailableLocales?
                .Where(l => l != null && l.Identifier.Code != "pseudo")
                .ToList();
            if (dropdownLocales == null || dropdownLocales.Count == 0) return;

            var options = new List<TMP_Dropdown.OptionData>(dropdownLocales.Count);
            foreach (var locale in dropdownLocales)
            {
                var label = string.IsNullOrEmpty(locale.LocaleName) ? locale.Identifier.Code : locale.LocaleName;
                options.Add(new TMP_Dropdown.OptionData(label));
            }
            dd.ClearOptions();
            dd.AddOptions(options);
            SyncDropdownSelection();
        }

        private void SyncDropdownSelection()
        {
            var dd = Component.LanguageDropdown;
            if (dd == null || dropdownLocales == null || LocalizationService == null) return;
            var current = LocalizationService.CurrentLocale;
            if (current == null) return;
            for (int i = 0; i < dropdownLocales.Count; i++)
            {
                if (dropdownLocales[i] != null && dropdownLocales[i].Identifier == current.Identifier)
                {
                    dd.SetValueWithoutNotify(i);
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
    }
}
