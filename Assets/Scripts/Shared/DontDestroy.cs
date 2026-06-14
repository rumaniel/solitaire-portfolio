
using UnityEngine;

namespace Shared
{
    public class DontDestroy : MonoBehaviour
    {
        private void Awake()
        {
            // Only mark the root — if any ancestor already handles DontDestroyOnLoad,
            // this object will move with it and calling it again triggers an assertion.
            if (transform.parent != null) return;
            DontDestroyOnLoad(gameObject);
        }
    }
}
