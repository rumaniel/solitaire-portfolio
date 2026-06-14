using System.Collections.Generic;
using Component.Card;
using Model.Game;

namespace Scene.Ingame
{
    /// <summary>
    /// The card controller (<see cref="UICardsController"/>) for each card game in the scene.
    /// Both table prefab instances live in the Ingame scene; <see cref="IngameComponent.ActivateLayout"/> activates
    /// the one matching the route's game type. A null entry (a table not yet wired) is tolerated
    /// and skipped by <see cref="All"/>.
    /// </summary>
    public sealed class CardViewSet
    {
        private readonly UICardsController klondike; // serves Klondike + Easthaven
        private readonly UICardsController spider;

        public CardViewSet(UICardsController klondike, UICardsController spider)
        {
            this.klondike = klondike;
            this.spider = spider;
        }

        /// <summary>Controller for the game type (Spider → spider table; everything else → klondike table).</summary>
        public UICardsController For(GameType gameType) =>
            gameType == GameType.Spider ? spider : klondike;

        /// <summary>All present controllers (nulls skipped) — used to skin and toggle visibility.</summary>
        public IEnumerable<UICardsController> All
        {
            get
            {
                if (klondike != null) yield return klondike;
                if (spider != null) yield return spider;
            }
        }
    }
}
