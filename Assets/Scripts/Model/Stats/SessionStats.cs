namespace Model.Stats
{
    public class SessionStats
    {
        public int Score;
        public int MoveCount;
        public float ElapsedSeconds;
        public bool UndoUsed;
        public bool HintUsed;
        public int HintCount;
        public bool IsWon;
        public bool IsFinished;

        public SessionStats Snapshot() => (SessionStats)MemberwiseClone();
    }
}
