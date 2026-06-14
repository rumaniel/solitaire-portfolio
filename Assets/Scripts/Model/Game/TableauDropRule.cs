namespace Model.Game
{
    /// <summary>How the moved card must relate to the tableau target's top card.</summary>
    public enum TableauDropRule
    {
        AlternatingColor, // Klondike/Easthaven: one rank lower, opposite color
        AnySuit,          // Spider: one rank lower, suit ignored
    }
}
