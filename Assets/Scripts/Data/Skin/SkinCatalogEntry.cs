using System;
using Model.Skin;
using UnityEngine;

namespace Data.Skin
{
    /// <summary>One skin's serialized metadata + Addressable reference. Inspector-bindable; public ctor for tests.</summary>
    [Serializable]
    public class SkinCatalogEntry
    {
        [SerializeField] private string id;
        [SerializeField] private string displayNameKey;
        [SerializeField] private Sprite thumbnail;
        [SerializeField] private CardSpriteSetReference spriteSetRef;

        public SkinCatalogEntry() { }

        public SkinCatalogEntry(string id, string displayNameKey, Sprite thumbnail, CardSpriteSetReference spriteSetRef)
        {
            this.id = id;
            this.displayNameKey = displayNameKey;
            this.thumbnail = thumbnail;
            this.spriteSetRef = spriteSetRef;
        }

        public SkinId Id => new SkinId(id);
        public string DisplayNameKey => displayNameKey;
        public Sprite Thumbnail => thumbnail;
        public CardSpriteSetReference SpriteSetRef => spriteSetRef;

        public SkinInfo ToInfo() => new SkinInfo(Id, displayNameKey, thumbnail);
    }
}
