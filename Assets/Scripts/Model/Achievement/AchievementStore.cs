using System.Collections.Generic;
using MemoryPack;

namespace Model.Achievement
{
    [MemoryPackable]
    public partial class AchievementStore
    {
        public List<AchievementStatus> Entries;

        public AchievementStore()
        {
            Entries = new List<AchievementStatus>();
        }
    }
}
