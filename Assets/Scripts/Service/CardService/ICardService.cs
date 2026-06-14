using System.Collections.Generic;
using Model.Card;
using Model.Game;
using Service.GameService;

namespace Service.CardService
{
    public interface ICardService
    {
        /// <summary>
        /// Injects the game rule and initializes the service.
        /// IDealRule is the single source of truth for card movement rules,
        /// so no separate class per game type is needed.
        /// </summary>
        void Initialize(IDealRule rule);

        /// <summary>
        /// Validates a move against current board state.
        /// TableState is required to check actual pile contents (e.g. empty tableau rule).
        /// </summary>
        MoveCardResult TryMove(MoveCardRequest request, TableState state);

        /// <summary>
        /// True when the bottom-to-top ordered cards form a legal pickup run under the active
        /// rule. The drag layer uses this to veto gestures without duplicating rule logic.
        /// </summary>
        bool IsValidRunPickup(IReadOnlyList<PlayingCard> run);
    }
}
