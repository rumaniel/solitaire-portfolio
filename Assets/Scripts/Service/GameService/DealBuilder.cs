using System;
using System.Collections.Generic;
using System.Linq;
using Model.Card;
using Model.Game;

namespace Service.GameService
{
    /// <summary>
    /// Builds a <see cref="TableState"/> from a shuffled deck and a <see cref="IDealRule"/>.
    /// Extracted from SolitaireGameService.Initialize() to allow reuse
    /// (e.g. seed-based replay, solver verification).
    /// </summary>
    public static class DealBuilder
    {
        /// <summary>
        /// Distributes a deck into Tableau, Stock, Waste, and Foundation piles
        /// according to the given deal rule.
        /// </summary>
        /// <exception cref="ArgumentNullException">If <paramref name="deck"/> or <paramref name="dealRule"/> is null.</exception>
        /// <exception cref="ArgumentException">If the deck is too small for the deal rule's tableau layout.</exception>
        public static TableState Build(List<PlayingCard> deck, IDealRule dealRule)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (dealRule == null) throw new ArgumentNullException(nameof(dealRule));
            if (dealRule.InitialCardCounts == null)
                throw new ArgumentException("DealRule.InitialCardCounts must not be null.", nameof(dealRule));
            if (dealRule.InitialCardCounts.Length < dealRule.TableauCount)
                throw new ArgumentException(
                    $"DealRule.InitialCardCounts has {dealRule.InitialCardCounts.Length} entries " +
                    $"but TableauCount is {dealRule.TableauCount}.",
                    nameof(dealRule));

            int requiredCards = dealRule.InitialCardCounts.Sum();
            if (deck.Count < requiredCards)
                throw new ArgumentException(
                    $"Deck has {deck.Count} cards but deal rule requires at least {requiredCards} for tableaus.",
                    nameof(deck));

            var cursor = 0;

            // --- Tableau ---
            var tableaus = new List<PileState>(dealRule.TableauCount);
            for (int col = 0; col < dealRule.TableauCount; col++)
            {
                int count = dealRule.InitialCardCounts[col];
                var cards = deck.GetRange(cursor, count);
                cursor += count;

                int faceUpFrom = Math.Max(0, count - dealRule.InitialFaceUpPerColumn);
                tableaus.Add(new PileState(new PileId(PileType.Tableau, col), cards, faceUpFrom));
            }

            // --- Stock (all remaining cards, all face-down) ---
            var stockCards = deck.GetRange(cursor, deck.Count - cursor);
            var stock = new PileState(
                new PileId(PileType.Stock, 0),
                stockCards,
                stockCards.Count); // faceUpFromIndex == Count -> all face-down

            // --- Waste (empty pile) ---
            var waste = new PileState(new PileId(PileType.Waste, 0), new List<PlayingCard>(), 0);

            // --- Foundations (empty piles x foundationCount) ---
            var foundations = new List<PileState>(dealRule.FoundationCount);
            for (int i = 0; i < dealRule.FoundationCount; i++)
                foundations.Add(new PileState(new PileId(PileType.Foundation, i), new List<PlayingCard>(), 0));

            return new TableState(stock, waste, foundations, tableaus);
        }
    }
}
