using Core;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.Lobby
{
    /// <summary>
    /// Lobby scene root. Configures DI for game selection screen.
    /// </summary>
    public class LobbyScene : SceneBase
    {
        [SerializeField] private LobbyComponent component;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(component);
            builder.RegisterEntryPoint<LobbyPresenter>().AsSelf();
        }
    }
}
