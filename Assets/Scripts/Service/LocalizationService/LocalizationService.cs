using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Service.LocalizationService
{
    public class LocalizationService : ILocalizationService, IDisposable
    {
        public const string PlayerPrefsLocaleKey = "i18n.locale";

        private readonly Subject<Locale> localeSubject = new();
        private Locale currentLocale;
        private UniTask? initializeTask;
        private bool initialized;
        private bool subscribed;
        private bool disposed;

        public Locale CurrentLocale => currentLocale;

        public IReadOnlyList<Locale> AvailableLocales
            => LocalizationSettings.AvailableLocales?.Locales ?? (IReadOnlyList<Locale>)Array.Empty<Locale>();

        public Observable<Locale> OnLocaleChanged => localeSubject;

        public UniTask InitializeAsync()
        {
            // Cached task dedups concurrent startup callers — otherwise SelectedLocaleChanged
            // could double-subscribe.
            if (disposed || initialized) return UniTask.CompletedTask;
            return initializeTask ??= InitializeInternalAsync();
        }

        private async UniTask InitializeInternalAsync()
        {
            // Subscribe before try: a bootstrap failure must still leave the event wired so
            // later SetLocaleAsync can emit OnLocaleChanged to observers.
            if (!subscribed)
            {
                LocalizationSettings.SelectedLocaleChanged += OnSettingsLocaleChanged;
                subscribed = true;
            }

            try
            {
                await LocalizationSettings.InitializationOperation.ToUniTask();

                var startup = ResolveStartupLocale();
                if (startup != null)
                {
                    LocalizationSettings.SelectedLocale = startup;
                    // SelectedLocaleAsync is per-change; InitializationOperation is one-time.
                    await LocalizationSettings.SelectedLocaleAsync.ToUniTask();
                    currentLocale = startup;
                }

                initialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Localization] InitializeAsync failed; falling back to English if available.");
                Debug.LogException(ex);
                var fallback = AvailableLocales.FirstOrDefault(l => l.Identifier.Code == "en")
                               ?? AvailableLocales.FirstOrDefault();
                // Push fallback through LocalizationSettings so LocalizeStringEvent actually renders it.
                if (fallback != null) LocalizationSettings.SelectedLocale = fallback;
                currentLocale = fallback;
                initialized = true;
            }
        }

        public async UniTask SetLocaleAsync(Locale locale)
        {
            if (disposed || locale == null) return;
            LocalizationSettings.SelectedLocale = locale;
            currentLocale = locale;
            PlayerPrefs.SetString(PlayerPrefsLocaleKey, locale.Identifier.Code);
            PlayerPrefs.Save();
            await LocalizationSettings.SelectedLocaleAsync.ToUniTask();
        }

        public async UniTask<string> GetStringAsync(string tableName, string key, params object[] args)
        {
            if (disposed || string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(key)) return key ?? string.Empty;
            try
            {
                var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, key, args);
                var result = await op.ToUniTask();
                return result ?? key;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return key;
            }
        }

        public async UniTask<string> GetStringAsync(LocalizedString entry, params object[] args)
        {
            if (disposed) return string.Empty;
            if (entry == null || entry.IsEmpty)
            {
                // Return a visible placeholder so the missing binding is detectable in a build,
                // not just in the Editor console.
                Debug.LogWarning("[Localization] GetStringAsync: LocalizedString unbound in Inspector.");
                return "[i18n:missing]";
            }
            try
            {
                // Route through entry so Inspector Arguments/LocalVariables are respected.
                UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<string> op =
                    (args == null || args.Length == 0)
                        ? entry.GetLocalizedStringAsync()
                        : entry.GetLocalizedStringAsync(args);
                var result = await op.ToUniTask();
                return result ?? string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return string.Empty;
            }
        }

        private Locale ResolveStartupLocale()
        {
            var locales = AvailableLocales;
            if (locales.Count == 0) return null;

            var savedCode = PlayerPrefs.GetString(PlayerPrefsLocaleKey, string.Empty);
            if (!string.IsNullOrEmpty(savedCode))
            {
                var match = locales.FirstOrDefault(l => l.Identifier.Code == savedCode);
                if (match != null) return match;
            }

            var osCode = MapSystemLanguage(Application.systemLanguage);
            var osMatch = locales.FirstOrDefault(l => l.Identifier.Code == osCode);
            if (osMatch != null) return osMatch;

            return locales.FirstOrDefault(l => l.Identifier.Code == "en") ?? locales[0];
        }

        /// <summary>Public so EditMode tests can exercise it without the LocalizationSettings pipeline.</summary>
        public static string MapSystemLanguage(SystemLanguage lang)
        {
            switch (lang)
            {
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.ChineseSimplified: return "zh-Hans";
                case SystemLanguage.ChineseTraditional: return "zh-Hant";
                case SystemLanguage.Chinese: return "zh";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Spanish: return "es";
                default: return "en";
            }
        }

        private void OnSettingsLocaleChanged(Locale newLocale)
        {
            currentLocale = newLocale;
            if (!disposed) localeSubject.OnNext(newLocale);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (subscribed)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnSettingsLocaleChanged;
                subscribed = false;
            }
            localeSubject.Dispose();
        }
    }
}
