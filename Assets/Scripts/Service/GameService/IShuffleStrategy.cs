using System.Collections.Generic;
using Model.Card;
using Model.Game;

namespace Service.GameService
{
    /// <summary>
    /// Produces a shuffled deck from a seed.
    /// Implementations must be deterministic: same seed always yields same card order.
    /// </summary>
    public interface IShuffleStrategy
    {
        List<PlayingCard> Shuffle(int seed);

        /// <summary>
        /// Shuffles the deck composition the rule prescribes (DeckCount x 52 remapped onto
        /// SuitCount suits). Default falls back to the classic 52-card shuffle so existing
        /// strategies stay source-identical.
        /// </summary>
        List<PlayingCard> Shuffle(int seed, IDealRule rule) => Shuffle(seed);
    }
}
