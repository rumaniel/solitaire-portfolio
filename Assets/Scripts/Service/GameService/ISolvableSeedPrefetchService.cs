using Model.Board;
using Model.Game;

namespace Service.GameService
{
    public interface ISolvableSeedPrefetchService
    {
        /// <summary>Returns and consumes the pre-resolved seed for this rule key, or null when none is ready.</summary>
        int? TryConsume(GameType gameType, int variant);
        /// <summary>Starts a background resolve for the next fresh deal of this rule. No-op while one is pending or already stored.</summary>
        void Prefetch(GameType gameType, int variant, IDealRule rule);
        /// <summary>Starts a background resolve for the next fresh board deal (Pyramid/TriPeaks). No-op while one is pending or already stored.</summary>
        void PrefetchBoard(GameType gameType, int variant, BoardLayout layout);
    }
}
