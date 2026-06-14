using System.Collections.Generic;
using Model.Card;
using UnityEngine;

namespace Data.Card
{
    public class CardSpriteLookup
    {
        private readonly Dictionary<int, Sprite> frontSprites;

        public CardSpriteLookup(IReadOnlyList<CardFaceSpriteEntry> entries)
        {
            frontSprites = new Dictionary<int, Sprite>(entries?.Count ?? 0);
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                frontSprites[ToKey(entry.Suit, entry.Rank)] = entry.Sprite;
            }
        }

        public bool TryGet(Suit suit, Rank rank, out Sprite sprite)
        {
            return frontSprites.TryGetValue(ToKey(suit, rank), out sprite);
        }

        public static int ToKey(Suit suit, Rank rank)
        {
            return ((int)suit << 8) | (int)rank;
        }
    }
}
