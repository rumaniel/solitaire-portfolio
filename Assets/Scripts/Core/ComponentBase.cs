using UnityEngine;
using VContainer.Unity;

namespace Core
{
    /// <summary>
    /// Basis of the <see cref="MonoBehaviour"/> in project.
    /// </summary>
    public abstract class ComponentBase : MonoBehaviour
    {
        /// <summary>
        /// Awake is called when the script instance is being loaded.
        /// </summary>
        protected virtual void Awake()
        {
            // Get the current active scene.
            var scene = gameObject.scene;

            // Call injection from scene provider.
            var provider = LifetimeScope.Find<SceneBase>(scene);

            // Null check for unity object.
            if (provider == null || provider.Container == null)
                return;

            // Inject dependencies.
            provider.Container.Inject(this);
        }
    }
}
