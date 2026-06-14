using R3;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Scene.Login
{
    public class LoginComponent : MonoBehaviour
    {
        [Header("Properties")]
        public SerializableReactiveProperty<string> UserId;
        public UnityEvent<string> OnUserIdChanged;
        public SerializableReactiveProperty<string> Status;
        public UnityEvent<string> OnStatusChanged;

        [Header("Events")]
        public UnityEvent OnStartClicked;

        [Header("Language Dropdown")]
        // Inspector-바인딩 우선: LoginPresenter는 LocalizationService를 통해 옵션을 채우고
        // onValueChanged 구독을 건다. 비워두면 dropdown 미사용으로 자연 비활성.
        public TMP_Dropdown LanguageDropdown;

        private void Awake()
        {
            UserId.AsObservable().Subscribe(value =>
            {
                OnUserIdChanged.Invoke(value);
            }).AddTo(this);

            Status.AsObservable().Subscribe(value =>
            {
                OnStatusChanged.Invoke(value);
            }).AddTo(this);
        }

        public void OnClickStart() => OnStartClicked.Invoke();

    }
}
