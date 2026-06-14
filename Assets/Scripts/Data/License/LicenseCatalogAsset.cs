using System.Collections.Generic;
using UnityEngine;

namespace Data.License
{
    /// <summary>Catalog of third-party license entries displayed in the Licenses panel.</summary>
    [CreateAssetMenu(menuName = "Solitaire/License/License Catalog", fileName = "LicenseCatalog")]
    public class LicenseCatalogAsset : ScriptableObject
    {
        [SerializeField] private LicenseEntry[] entries;

        public IReadOnlyList<LicenseEntry> Entries => entries;
    }
}
