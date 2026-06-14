using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data.Card;
using Data.Skin;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Gateway.Skin
{
    /// <summary>
    /// Loads CardSpriteSet assets through each entry's AssetReference using the Addressables
    /// load/release idiom (LoadAssetAsync + ReleaseAsset). Caches the in-flight
    /// <see cref="AsyncOperationHandle{T}"/> per reference so concurrent loads share the same
    /// operation, and clears the cache on failure so a retry is possible.
    /// </summary>
    public class AddressableSkinAssetGateway : ISkinAssetGateway
    {
        private readonly Dictionary<CardSpriteSetReference, AsyncOperationHandle<CardSpriteSet>> handles = new();

        public async UniTask<CardSpriteSet> LoadAsync(CardSpriteSetReference reference)
        {
            // If a load is already in flight (or completed) for this reference, await its handle.
            // This both prevents the "already loaded" error from a second LoadAssetAsync call AND
            // avoids returning reference.Asset (null) before the first load completes.
            if (handles.TryGetValue(reference, out var existing))
                return await existing.ToUniTask();

            var handle = reference.LoadAssetAsync<CardSpriteSet>();
            handles[reference] = handle;
            try
            {
                return await handle.ToUniTask();
            }
            catch
            {
                // Failure path: drop the entry so a subsequent retry can issue a fresh
                // LoadAssetAsync, and release the AssetReference's internal handle so it isn't
                // stuck in a half-loaded state for the next attempt.
                handles.Remove(reference);
                reference.ReleaseAsset();
                throw;
            }
        }

        public void Release(CardSpriteSetReference reference)
        {
            if (!handles.Remove(reference)) return;
            reference.ReleaseAsset();
        }
    }
}
