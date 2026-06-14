using Model.Game;

namespace Service.GameService
{
    /// <summary>Generates an initial <see cref="TableState"/> directly, bypassing the Shuffle + DealBuilder roundtrip.</summary>
    public interface IReversePlayStrategy
    {
        TableState BuildInitialState(int seed, IDealRule dealRule);
    }
}
