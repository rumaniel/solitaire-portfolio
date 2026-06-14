using System;

namespace Service.GameService
{
    /// <summary>
    /// Mutable holder for the active <see cref="IShuffleStrategy"/>.
    /// Swap <see cref="Current"/> at any time; the next
    /// <see cref="IGameService.Initialize"/> call will use it.
    /// </summary>
    public class ShuffleStrategyProvider
    {
        private IShuffleStrategy current = new FisherYatesShuffleStrategy();

        public IShuffleStrategy Current
        {
            get => current;
            set => current = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}
