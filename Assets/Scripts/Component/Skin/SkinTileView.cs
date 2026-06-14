using Data.Skin;
using Model.Skin;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Component.Skin
{
    /// <summary>
    /// One selectable skin tile. Base prefab "SkinTile" → variants placed in the grid
    /// (Prefab Variant 우선 원칙, GameTileView와 동일 결).
    /// </summary>
    public class SkinTileView : MonoBehaviour
    {
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private Button button;
        [SerializeField] private GameObject selectedIndicator;

        private readonly Subject<SkinId> onClickedSubject = new Subject<SkinId>();
        public Observable<SkinId> OnClickedObservable() => onClickedSubject;

        private SkinId skinId;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => onClickedSubject.OnNext(skinId));
        }

        public void Bind(SkinInfo info)
        {
            skinId = info.Id;
            if (thumbnailImage != null) thumbnailImage.sprite = info.Thumbnail;
        }

        public void SetSelected(bool selected)
        {
            if (selectedIndicator != null) selectedIndicator.SetActive(selected);
        }

        private void OnDestroy() => onClickedSubject.Dispose();
    }
}
