using System;
using Data.Card;
using UnityEngine.AddressableAssets;

namespace Data.Skin
{
    /// <summary>
    /// Typed Addressable reference to a <see cref="CardSpriteSet"/>. Concrete subclass so the
    /// Inspector draws a type-filtered object picker (raw AssetReferenceT&lt;T&gt; does not).
    /// </summary>
    [Serializable]
    public class CardSpriteSetReference : AssetReferenceT<CardSpriteSet>
    {
        public CardSpriteSetReference(string guid) : base(guid) { }
    }
}
