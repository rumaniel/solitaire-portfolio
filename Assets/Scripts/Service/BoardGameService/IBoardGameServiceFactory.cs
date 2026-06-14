using Model.Game;

namespace Service.BoardGameService
{
    /// <summary>Resolves the board game service for a given <see cref="GameType"/> (Pyramid / TriPeaks).
    /// Both services are registered per-scene; the presenter creates the one matching the route.</summary>
    public interface IBoardGameServiceFactory
    {
        IBoardGameService Create(GameType gameType);
    }
}
