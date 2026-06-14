using Data.License;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Component.Settings
{
    /// <summary>Sub-panel listing third-party licenses from <see cref="LicenseCatalogAsset"/>.</summary>
    public class LicensesPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private LicenseEntryRow rowPrefab;
        [SerializeField] private LicenseCatalogAsset catalog;

        private void Awake()
        {
            closeButton?.OnClickAsObservable().Subscribe(_ => Hide()).AddTo(this);
        }

        public void Show()
        {
            if (panel == null) return;
            Populate();
            panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void TriggerBack() => Hide();

        // Catalog is static, so rows are created once and kept for the component's lifetime.
        private void Populate()
        {
            if (contentRoot == null || rowPrefab == null || catalog == null) return;
            if (contentRoot.childCount > 0) return;

            var entries = catalog.Entries;
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                var row = Instantiate(rowPrefab, contentRoot);
                row.Apply(entries[i]);
            }
        }
    }
}
