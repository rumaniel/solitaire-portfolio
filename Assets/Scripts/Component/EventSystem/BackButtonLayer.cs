using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Component.EventSystem
{
    /// <summary>
    /// Layered back-button handler. Only the topmost active layer
    /// processes Android back / PC Escape. Self-registers on OnEnable,
    /// unregisters on OnDisable — attach to a panel's child GameObject
    /// so SetActive toggling automatically manages the stack.
    /// When no layer is registered, back press quits the application
    /// (replacing the legacy Input.backButtonLeavesApp = true behavior).
    /// </summary>
    [AddComponentMenu("Input/Back Button Layer")]
    [DisallowMultipleComponent]
    public class BackButtonLayer : MonoBehaviour
    {
        private static readonly List<BackButtonLayer> layers = new();
        private static InputAction backAction;

        [SerializeField] private UnityEvent onBack;

        public UnityEvent OnBack => onBack;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            layers.Clear();
            if (backAction != null)
            {
                backAction.performed -= OnBackPerformed;
                backAction.Disable();
                backAction.Dispose();
                backAction = null;
            }

            // Unity routes Android Back to KeyCode.Escape, so a single keyboard
            // binding covers both PC ESC and Android back without extra config.
            backAction = new InputAction(name: "Back", type: InputActionType.Button, binding: "<Keyboard>/escape");
            backAction.performed += OnBackPerformed;
            backAction.Enable();
        }

        private static void OnBackPerformed(InputAction.CallbackContext _)
        {
            if (layers.Count == 0)
            {
                Application.Quit();
                return;
            }
            layers[layers.Count - 1].onBack?.Invoke();
        }

        private void OnEnable() => layers.Add(this);
        private void OnDisable() => layers.Remove(this);
    }
}
