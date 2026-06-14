using R3;

namespace Service.LayoutService
{
    public interface ILayoutService
    {
        bool IsLeftHanded { get; }
        Observable<bool> OnLeftHandedChanged { get; }

        void SetLeftHanded(bool leftHanded);
    }
}
