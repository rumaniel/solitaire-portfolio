using System.Collections.Generic;
using Model.Card;
using Model.Game;

namespace Service.GameService
{
    /// <summary>Default shuffle: Fisher-Yates via <see cref="DeckFactory.CreateShuffled"/>.</summary>
    public class FisherYatesShuffleStrategy : IShuffleStrategy
    {
        public List<PlayingCard> Shuffle(int seed) => DeckFactory.CreateShuffled(seed);

        public List<PlayingCard> Shuffle(int seed, IDealRule rule) => DeckFactory.CreateShuffled(seed, rule);
    }
}
