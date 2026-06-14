using System.Collections.Generic;
using Data.Skin;
using Model.Skin;

namespace Tests.EditMode
{
    /// <summary>Programmatic ISkinCatalog for tests — no ScriptableObject required.</summary>
    internal class FakeSkinCatalog : ISkinCatalog
    {
        private readonly List<SkinInfo> infos = new();
        private readonly Dictionary<string, SkinCatalogEntry> byId = new();

        public FakeSkinCatalog Add(SkinCatalogEntry entry)
        {
            infos.Add(entry.ToInfo());
            byId[entry.Id.Value ?? string.Empty] = entry;
            return this;
        }

        public IReadOnlyList<SkinInfo> Skins => infos;
        public bool TryGet(SkinId id, out SkinCatalogEntry entry) => byId.TryGetValue(id.Value ?? string.Empty, out entry);
        public bool Contains(SkinId id) => byId.ContainsKey(id.Value ?? string.Empty);
    }
}
