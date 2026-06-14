namespace Model.Achievement
{
    public readonly struct AchievementUnlockedEvent
    {
        public readonly string Id;
        public readonly long UnlockedAtUnix;
        public readonly bool Retroactive;

        public AchievementUnlockedEvent(string id, long unlockedAtUnix, bool retroactive)
        {
            Id = id;
            UnlockedAtUnix = unlockedAtUnix;
            Retroactive = retroactive;
        }
    }
}
