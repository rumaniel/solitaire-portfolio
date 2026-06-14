using System.Collections.Generic;
using Model.Skin;

namespace Data.Skin
{
    /// <summary>Read-only access to available skins. Implemented by SkinCatalogAsset (and test doubles).</summary>
    public interface ISkinCatalog
    {
        IReadOnlyList<SkinInfo> Skins { get; }
        bool TryGet(SkinId id, out SkinCatalogEntry entry);
        bool Contains(SkinId id);
    }
}
