namespace Model.Game
{
    /// <summary>What makes a multi-card tableau pickup a legal run.</summary>
    public enum TableauRunRule
    {
        AlternatingColor, // Klondike/Easthaven: descending, colors alternate
        SameSuit,         // Spider: descending, single suit
    }
}
