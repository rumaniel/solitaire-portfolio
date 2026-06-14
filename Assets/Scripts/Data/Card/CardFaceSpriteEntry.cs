using System;
using Model.Card;
using UnityEngine;

namespace Data.Card
{
    [Serializable]
    public struct CardFaceSpriteEntry
    {
        [SerializeField] private Suit suit;
        [SerializeField] private Rank rank;
        [SerializeField] private Sprite sprite;

        public Suit Suit => suit;
        public Rank Rank => rank;
        public Sprite Sprite => sprite;
    }
}
