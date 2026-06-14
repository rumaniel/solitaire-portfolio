using Model.Game;

namespace Service.GameService
{
    /// <summary>Creates an IDealRule from a GameType and variant identifier.</summary>
    public interface IDealRuleFactory
    {
        /// <summary>Returns the deal rule for the given game type and variant.</summary>
        /// <param name="variant">Game-type-specific variant id (e.g. Klondike drawCount). Defaults to 1.</param>
        IDealRule Create(GameType gameType, int variant = 1);
    }
}
