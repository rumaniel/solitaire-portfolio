using System.Collections.Generic;
using Data.Skin;
using Model.Skin;
using R3;
using UnityEngine;

namespace Component.Skin
{
    /// <summary>
    /// Builds a grid of SkinTileView from the available skins and surfaces selection clicks.
    /// Highlights the current skin. Token-light: thumbnails come from the catalog (already bundled).
    /// </summary>
    public class SkinSelectView : MonoBehaviour
    {
        [SerializeField] private Transform tileParent;
        [SerializeField] private SkinTileView tilePrefab;

        private readonly List<SkinTileView> tiles = new List<SkinTileView>();
        private readonly Subject<SkinId> onSkinSelectedSubject = new Subject<SkinId>();
        private readonly Dictionary<SkinTileView, SkinId> tileIds = new Dictionary<SkinTileView, SkinId>();

        public Observable<SkinId> OnSkinSelectedObservable() => onSkinSelectedSubject;

        public void Build(IReadOnlyList<SkinInfo> skins)
        {
            Clear();
            if (tilePrefab == null || tileParent == null) return;

            foreach (var info in skins)
            {
                var tile = Instantiate(tilePrefab, tileParent);
                tile.Bind(info);
                tileIds[tile] = info.Id;
                tile.OnClickedObservable()
                    .Subscribe(id => onSkinSelectedSubject.OnNext(id))
                    .AddTo(tile);
                tiles.Add(tile);
            }
        }

        public void SetSelected(SkinId currentId)
        {
            foreach (var tile in tiles)
            {
                if (tile == null) continue;
                tile.SetSelected(tileIds.TryGetValue(tile, out var id) && id.Equals(currentId));
            }
        }

        private void Clear()
        {
            foreach (var tile in tiles)
                if (tile != null) Destroy(tile.gameObject);
            tiles.Clear();
            tileIds.Clear();
        }

        private void OnDestroy()
        {
            onSkinSelectedSubject.Dispose();
        }
    }
}
