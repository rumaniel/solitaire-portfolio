using System.Collections.Generic;
using Model.Achievement;
using UnityEngine;

namespace Data.Achievement
{
    /// <summary>Catalog of all achievement definitions. Single instance registered in AppLifetimeScope.</summary>
    [CreateAssetMenu(fileName = "AchievementCatalog", menuName = "Solitaire/Achievement/Catalog")]
    public class AchievementCatalogAsset : ScriptableObject, IAchievementCatalog
    {
        [SerializeField] private AchievementDefinitionAsset[] definitions;

        private IReadOnlyList<IAchievementDefinition> cachedDefinitions;
        private Dictionary<string, IAchievementDefinition> cachedById;

        public IReadOnlyList<IAchievementDefinition> Definitions
        {
            get
            {
                EnsureCache();
                return cachedDefinitions;
            }
        }

        public bool TryGet(string id, out IAchievementDefinition definition)
        {
            EnsureCache();
            return cachedById.TryGetValue(id, out definition);
        }

        private void EnsureCache()
        {
            if (cachedDefinitions != null) return;

            var list = new List<IAchievementDefinition>(definitions?.Length ?? 0);
            cachedById = new Dictionary<string, IAchievementDefinition>();
            if (definitions != null)
            {
                foreach (var def in definitions)
                {
                    if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                    if (cachedById.ContainsKey(def.Id)) continue;
                    list.Add(def);
                    cachedById[def.Id] = def;
                }
            }
            cachedDefinitions = list;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            cachedDefinitions = null;
            cachedById = null;
            if (definitions == null) return;

            var seen = new HashSet<string>();
            foreach (var def in definitions)
            {
                if (def == null) continue;
                var id = def.Id;
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogError($"[{name}] Definition '{def.name}' has empty Id.", def);
                    continue;
                }
                if (!seen.Add(id))
                    Debug.LogError($"[{name}] Duplicate achievement id '{id}' in catalog.", def);
            }
        }
#endif
    }
}
