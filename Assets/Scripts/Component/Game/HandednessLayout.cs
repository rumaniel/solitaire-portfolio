using Core;
using R3;
using Service.LayoutService;
using UnityEngine;
using VContainer;

namespace Component.Game
{
    /// <summary>Activates one of two pre-laid-out toolbar roots based on
    /// `ILayoutService.IsLeftHanded`. Both roots stay parented in the scene as Prefab Variants of
    /// the bottom toolbar; only one is active at a time. The toggle is the Settings panel's
    /// `leftHandedToggle`.
    ///
    /// SCOPE: only the bottom toolbar (hint / undo / new game buttons + dragging affordance) is
    /// swap-eligible. The HUD (score / time / moves) lives outside this swap because
    /// `IngameComponent.hudView` is a single SerializeField — putting two HUD copies inside the
    /// swap would mean the inactive copy never receives presenter updates.</summary>
    public class HandednessLayout : ComponentBase
    {
        // Each root is a Prefab Variant of the same toolbar with anchors mirrored for left vs
        // right thumb. Wire both in Inspector. Do NOT include the HUD inside either root —
        // the presenter's single `hudView` reference can't follow a swap.
        [SerializeField] private GameObject rightHandedRoot;
        [SerializeField] private GameObject leftHandedRoot;

        [Inject] private ILayoutService LayoutService { get; set; }

        private readonly CompositeDisposable disposable = new();

        protected override void Awake()
        {
            base.Awake();
            if (LayoutService == null) return;

            ApplyLeftHanded(LayoutService.IsLeftHanded);
            LayoutService.OnLeftHandedChanged
                .Subscribe(ApplyLeftHanded)
                .AddTo(disposable);
        }

        private void OnDestroy()
        {
            disposable.Dispose();
        }

        private void ApplyLeftHanded(bool leftHanded)
        {
            if (rightHandedRoot != null) rightHandedRoot.SetActive(!leftHanded);
            if (leftHandedRoot != null) leftHandedRoot.SetActive(leftHanded);
        }
    }
}
