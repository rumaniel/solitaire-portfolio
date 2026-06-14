namespace Model.Achievement
{
    public enum AchievementRuleType
    {
        None = 0,
        FirstWin = 1,
        TotalWinsAtLeast = 2,
        WinStreakAtLeast = 3,
        ShortestWinUnderSeconds = 4,
        NoHintWin = 5,
        NoUndoWin = 6,
        PerfectRun = 7,
        DailyCompletedAtLeast = 8,
        DailyStreakAtLeast = 9,
    }
}
