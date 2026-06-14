using System.Collections.Generic;
using Model.Skin;
using UnityEngine;

namespace Data.Skin
{
    /// <summary>Builds id→entry and ordered SkinInfo lookups from catalog entries. Pure, testable. Mirrors CardSpriteLookup.</summary>
    public sealed class SkinCatalogLookup
    {
        private readonly List<SkinInfo> infos = new List<SkinInfo>();
        private readonly Dictionary<string, SkinCatalogEntry> byId = new Dictionary<string, SkinCatalogEntry>();

        public SkinCatalogLookup(IReadOnlyList<SkinCatalogEntry> entries)
        {
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (entry == null) continue;
                var key = entry.Id.Value ?? string.Empty;
                if (byId.ContainsKey(key))
                {
                    Debug.LogWarning($"[SkinCatalog] Duplicate skin id detected: '{key}'");
                    continue;
                }
                byId[key] = entry;
                infos.Add(entry.ToInfo());
            }
        }

        public IReadOnlyList<SkinInfo> Skins => infos;

        public bool TryGet(SkinId id, out SkinCatalogEntry entry)
            => byId.TryGetValue(id.Value ?? string.Empty, out entry);

        public bool Contains(SkinId id) => byId.ContainsKey(id.Value ?? string.Empty);
    }
}
