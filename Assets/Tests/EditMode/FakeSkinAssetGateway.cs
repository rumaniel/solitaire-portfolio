using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data.Card;
using Data.Skin;
using Gateway.Skin;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>In-memory gateway: returns a distinct CardSpriteSet per reference, records Release calls.</summary>
    internal class FakeSkinAssetGateway : ISkinAssetGateway
    {
        private readonly Dictionary<CardSpriteSetReference, CardSpriteSet> assets = new();
        public readonly List<CardSpriteSetReference> Released = new();
        public int LoadCalls { get; private set; }

        private CardSpriteSet GetOrCreate(CardSpriteSetReference reference)
        {
            if (!assets.TryGetValue(reference, out var set))
            {
                set = ScriptableObject.CreateInstance<CardSpriteSet>();
                assets[reference] = set;
            }
            return set;
        }

        public UniTask<CardSpriteSet> LoadAsync(CardSpriteSetReference reference)
        {
            LoadCalls++;
            return UniTask.FromResult(GetOrCreate(reference));
        }

        public void Release(CardSpriteSetReference reference) => Released.Add(reference);
    }
}
