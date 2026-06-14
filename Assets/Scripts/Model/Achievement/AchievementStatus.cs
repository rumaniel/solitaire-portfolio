using MemoryPack;

namespace Model.Achievement
{
    [MemoryPackable]
    public partial class AchievementStatus
    {
        public string Id;
        public AchievementState State;
        public long UnlockedAtUnix;
        public int CurrentProgress;

        public AchievementStatus()
        {
            Id = string.Empty;
        }
    }
}
