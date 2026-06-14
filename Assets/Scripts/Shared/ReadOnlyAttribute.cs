using UnityEngine;

namespace Shared
{
    /// <summary>
    /// Marks a field as read-only in the Unity Inspector.
    /// Fields decorated with this attribute will be displayed but cannot be edited during runtime or in the editor.
    /// This is useful for showing calculated values, debug information, or other data that should be visible but not modifiable.
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute
    {

    }
}

