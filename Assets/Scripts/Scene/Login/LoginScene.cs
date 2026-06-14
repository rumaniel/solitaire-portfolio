using Component.Consent;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Core;

namespace Scene.Login
{
    public class LoginScene : SceneBase
    {
        [SerializeField] private LoginComponent component;
        [SerializeField] private ConsentDialogView consentDialog;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(component);
            // Consent dialog lives in the Login scene so it can use the language dropdown
            // exposed by LoginComponent and so App.asmdef does not need a Component reference.
            // Fail-fast in line with appConfig/achievementCatalog wiring requirements.
            if (consentDialog == null)
            {
                throw new System.InvalidOperationException(
                    "[LoginScene] consentDialog reference is missing — wire ConsentDialog in the Login scene.");
            }
            builder.RegisterComponent(consentDialog);

            builder.RegisterEntryPoint<LoginPresenter>().As<LoginPresenter>();
        }
    }
}
