using Model.Skin;

namespace Service.SkinService
{
    /// <summary>Persists the selected skin id. Abstracted over PlayerPrefs for testability.</summary>
    public interface ISkinPreferenceStore
    {
        bool TryLoad(out SkinId id);
        void Save(SkinId id);
    }
}
