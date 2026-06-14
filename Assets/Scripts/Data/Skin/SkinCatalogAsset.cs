using System.Collections.Generic;
using Model.Skin;
using UnityEngine;

namespace Data.Skin
{
    [CreateAssetMenu(fileName = "SkinCatalog", menuName = "Solitaire/Skin/Catalog")]
    public class SkinCatalogAsset : ScriptableObject, ISkinCatalog
    {
        [SerializeField] private List<SkinCatalogEntry> skins = new List<SkinCatalogEntry>();

        private SkinCatalogLookup lookup;

        public IReadOnlyList<SkinInfo> Skins
        {
            get { EnsureLookup(); return lookup.Skins; }
        }

        public bool TryGet(SkinId id, out SkinCatalogEntry entry)
        {
            EnsureLookup();
            return lookup.TryGet(id, out entry);
        }

        public bool Contains(SkinId id)
        {
            EnsureLookup();
            return lookup.Contains(id);
        }

        private void OnEnable() => Build();
        private void OnValidate() => Build();

        private void EnsureLookup()
        {
            if (lookup == null) Build();
        }

        private void Build() => lookup = new SkinCatalogLookup(skins);
    }
}
