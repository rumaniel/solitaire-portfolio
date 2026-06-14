using R3;
using UnityEngine;

namespace Service.LayoutService
{
    public class LayoutService : ILayoutService
    {
        private const string LeftHandedKey = "layout.left_handed";

        private readonly Subject<bool> onLeftHandedChanged = new();
        private bool leftHanded;

        public LayoutService()
        {
            leftHanded = PlayerPrefs.GetInt(LeftHandedKey, 0) == 1;
        }

        public bool IsLeftHanded => leftHanded;
        public Observable<bool> OnLeftHandedChanged => onLeftHandedChanged;

        public void SetLeftHanded(bool value)
        {
            if (leftHanded == value) return;
            leftHanded = value;
            PlayerPrefs.SetInt(LeftHandedKey, value ? 1 : 0);
            onLeftHandedChanged.OnNext(value);
        }
    }
}
