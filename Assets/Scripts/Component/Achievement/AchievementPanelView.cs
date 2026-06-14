using System.Collections.Generic;
using Core;
using R3;
using Service.AchievementService;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Component.Achievement
{
    /// <summary>Browse panel for the full achievement catalog. Self-contained — pulls state from <see cref="IAchievementService"/>.</summary>
    public class AchievementPanelView : ComponentBase
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform itemContainer;
        [SerializeField] private AchievementItemView itemPrefab;
        [SerializeField] private Button closeButton;

        [Inject] private IAchievementService AchievementService { get; set; }

        private readonly List<AchievementItemView> pooled = new();
        private readonly CompositeDisposable subscription = new();
        private bool built;

        protected override void Awake()
        {
            base.Awake();
            closeButton?.OnClickAsObservable().Subscribe(_ => Hide()).AddTo(this);
        }

        public void Show()
        {
            if (panel == null) return;
            if (!built)
            {
                BuildItems();
                built = true;
            }
            RefreshAll();

            // Keep the open panel fresh if a background unlock fires (e.g. daily stats change).
            subscription.Clear();
            if (AchievementService != null)
            {
                AchievementService.OnProgressChanged
                    .Subscribe(_ => RefreshAll())
                    .AddTo(subscription);
            }
            panel.SetActive(true);
        }

        public void Hide()
        {
            subscription.Clear();
            if (panel != null) panel.SetActive(false);
        }

        private void BuildItems()
        {
            if (itemPrefab == null || itemContainer == null || AchievementService == null) return;

            // Drop any template children left in the prefab so the runtime grid starts clean.
            for (int i = itemContainer.childCount - 1; i >= 0; i--)
                Destroy(itemContainer.GetChild(i).gameObject);

            var all = AchievementService.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                var item = Instantiate(itemPrefab, itemContainer);
                pooled.Add(item);
            }
        }

        private void RefreshAll()
        {
            if (AchievementService == null) return;
            var all = AchievementService.GetAll();
            for (int i = 0; i < pooled.Count && i < all.Count; i++)
            {
                pooled[i].Render(all[i].Definition, all[i].Status);
            }
        }

        private void OnDestroy() => subscription.Dispose();
    }
}
