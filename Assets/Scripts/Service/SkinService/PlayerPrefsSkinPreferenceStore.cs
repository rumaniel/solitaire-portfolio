using Model.Skin;
using UnityEngine;

namespace Service.SkinService
{
    public class PlayerPrefsSkinPreferenceStore : ISkinPreferenceStore
    {
        private const string SkinIdKey = "skin.selected_id";

        public bool TryLoad(out SkinId id)
        {
            var raw = PlayerPrefs.GetString(SkinIdKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                id = default;
                return false;
            }
            id = new SkinId(raw);
            return true;
        }

        public void Save(SkinId id)
        {
            PlayerPrefs.SetString(SkinIdKey, id.Value ?? string.Empty);
            PlayerPrefs.Save();
        }
    }
}
