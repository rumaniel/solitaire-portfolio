using System.Collections.Generic;
using Model.Achievement;

namespace Tests.EditMode
{
    /// <summary>Programmatic <see cref="IAchievementCatalog"/> for tests — no ScriptableObject required.</summary>
    internal class StubAchievementCatalog : IAchievementCatalog
    {
        private readonly List<IAchievementDefinition> definitions = new();
        private readonly Dictionary<string, IAchievementDefinition> byId = new();

        public IReadOnlyList<IAchievementDefinition> Definitions => definitions;

        public StubAchievementCatalog Add(IAchievementDefinition def)
        {
            definitions.Add(def);
            byId[def.Id] = def;
            return this;
        }

        public bool TryGet(string id, out IAchievementDefinition definition)
            => byId.TryGetValue(id, out definition);
    }
}
