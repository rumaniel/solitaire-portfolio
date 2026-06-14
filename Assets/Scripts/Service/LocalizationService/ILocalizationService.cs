using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.Localization;

namespace Service.LocalizationService
{
    /// <summary>R3/UniTask wrapper over Unity.Localization — hides <c>LocalizationSettings</c> statics from consumers.</summary>
    public interface ILocalizationService
    {
        /// <summary>Startup locale: saved PlayerPrefs → OS language → English. Fails soft.</summary>
        UniTask InitializeAsync();

        Locale CurrentLocale { get; }
        IReadOnlyList<Locale> AvailableLocales { get; }
        Observable<Locale> OnLocaleChanged { get; }

        UniTask SetLocaleAsync(Locale locale);

        UniTask<string> GetStringAsync(string tableName, string key, params object[] args);
        UniTask<string> GetStringAsync(LocalizedString entry, params object[] args);
    }
}
