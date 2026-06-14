namespace Model.Achievement
{
    /// <summary>Lifecycle state of an achievement. Extensible (Claimed planned for future reward systems).</summary>
    public enum AchievementState
    {
        Locked = 0,
        Unlocked = 1,
    }
}
