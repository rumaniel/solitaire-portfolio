using Data.License;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;

namespace Component.Settings
{
    /// <summary>Single row in the Licenses panel — name/author, license label, full text.</summary>
    public class LicenseEntryRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private LocalizeStringEvent nameLocalizer;
        [SerializeField] private TMP_Text licenseText;
        [SerializeField] private TMP_Text fullText;

        public void Apply(LicenseEntry entry)
        {
            if (entry == null) return;

            if (nameText != null)
            {
                if (string.IsNullOrEmpty(entry.Author))
                {
                    // Disable the localizer so its "{0} — by {1}" template doesn't re-apply on locale change.
                    if (nameLocalizer != null) nameLocalizer.enabled = false;
                    nameText.text = entry.Name;
                }
                else if (nameLocalizer != null)
                {
                    nameLocalizer.enabled = true;
                    nameLocalizer.StringReference.Arguments = new object[] { entry.Name, entry.Author };
                    nameLocalizer.RefreshString();
                }
                else
                {
                    nameText.text = $"{entry.Name} — by {entry.Author}";
                }
            }
            if (licenseText != null) licenseText.text = entry.LicenseName;
            if (fullText != null) fullText.text = entry.FullText;
        }
    }
}
