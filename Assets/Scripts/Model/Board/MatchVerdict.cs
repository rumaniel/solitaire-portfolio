namespace Model.Board
{
    /// <summary>Result of evaluating the player's current ordered selection against a game's match rule.</summary>
    public enum MatchVerdict
    {
        /// <summary>Valid prefix; awaiting more taps.</summary>
        Incomplete,
        /// <summary>The selection is a complete, removable set.</summary>
        Match,
        /// <summary>The selection cannot form a match; the service resets it.</summary>
        Invalid
    }
}
