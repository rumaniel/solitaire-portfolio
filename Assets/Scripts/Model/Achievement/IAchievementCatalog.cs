using System.Collections.Generic;

namespace Model.Achievement
{
    public interface IAchievementCatalog
    {
        IReadOnlyList<IAchievementDefinition> Definitions { get; }
        bool TryGet(string id, out IAchievementDefinition definition);
    }
}
