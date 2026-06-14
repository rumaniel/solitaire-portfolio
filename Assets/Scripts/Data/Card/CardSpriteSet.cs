using System.Collections.Generic;
using Model.Card;
using UnityEngine;

namespace Data.Card
{
    [CreateAssetMenu(fileName = "CardSpriteSet", menuName = "Solitaire/Card/Sprite Set")]
    public class CardSpriteSet : ScriptableObject
    {
        [SerializeField] private Sprite backSprite;
        [SerializeField] private List<CardFaceSpriteEntry> frontSprites = new List<CardFaceSpriteEntry>();

        private CardSpriteLookup lookup;

        public Sprite BackSprite => backSprite;

        public bool TryGetFrontSprite(PlayingCard card, out Sprite sprite)
        {
            if (card == null)
            {
                sprite = null;
                return false;
            }

            EnsureLookup();
            return lookup.TryGet(card.Suit, card.Rank, out sprite);
        }

        public bool TryGetFrontSprite(Suit suit, Rank rank, out Sprite sprite)
        {
            EnsureLookup();
            return lookup.TryGet(suit, rank, out sprite);
        }

        private void OnEnable()
        {
            BuildLookup();
        }

        private void OnValidate()
        {
            BuildLookup();
            ValidateDuplicates();
        }

        private void EnsureLookup()
        {
            if (lookup == null)
            {
                BuildLookup();
            }
        }

        private void BuildLookup()
        {
            lookup = new CardSpriteLookup(frontSprites);
        }

        private void ValidateDuplicates()
        {
            if (frontSprites == null || frontSprites.Count == 0)
            {
                return;
            }

            var seen = new HashSet<int>();
            for (var i = 0; i < frontSprites.Count; i++)
            {
                var entry = frontSprites[i];
                var key = CardSpriteLookup.ToKey(entry.Suit, entry.Rank);
                if (!seen.Add(key))
                {
                    Debug.LogWarning($"Duplicate card sprite entry detected: {entry.Suit} {entry.Rank}", this);
                }
            }
        }
    }
}
