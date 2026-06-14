using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Component.Helpers
{
    /// <summary>One-liner setters for <see cref="LocalizeStringEvent"/> local variables.</summary>
    public static class LocalizationVariableHelpers
    {
        public static void SetIntVar(this LocalizeStringEvent localizer, string name, int value)
        {
            if (localizer == null) return;
            if (!WarnIfUnbound(localizer)) return;
            if (!TryGetTyped<IntVariable>(localizer, name, out var iv)) return;
            iv.Value = value;
            localizer.RefreshString();
        }

        public static void SetStringVar(this LocalizeStringEvent localizer, string name, string value)
        {
            if (localizer == null) return;
            if (!WarnIfUnbound(localizer)) return;
            if (!TryGetTyped<StringVariable>(localizer, name, out var sv)) return;
            sv.Value = value ?? string.Empty;
            localizer.RefreshString();
        }

        private static bool WarnIfUnbound(LocalizeStringEvent localizer)
        {
            if (localizer.StringReference != null && !localizer.StringReference.IsEmpty) return true;
            Debug.LogWarning(
                $"[Localization] {localizer.gameObject.name}: StringReference unbound in Inspector.",
                localizer);
            return false;
        }

        private static bool TryGetTyped<T>(LocalizeStringEvent localizer, string name, out T variable)
            where T : class
        {
            if (localizer.StringReference.TryGetValue(name, out var v) && v is T typed)
            {
                variable = typed;
                return true;
            }
            Debug.LogWarning(
                $"[Localization] {localizer.gameObject.name}: LocalVariable '{name}' missing or not {typeof(T).Name}.",
                localizer);
            variable = null;
            return false;
        }
    }
}
