using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data.Card;
using Data.Skin;
using Gateway.Skin;
using Model.Skin;
using R3;
using UnityEngine;

namespace Service.SkinService
{
    public class SkinService : ISkinService, IDisposable
    {
        public static readonly SkinId DefaultSkinId = new SkinId("classic");

        private readonly ISkinCatalog catalog;
        private readonly ISkinAssetGateway gateway;
        private readonly ISkinPreferenceStore preferences;

        private readonly ReactiveProperty<SkinId> currentSkinId = new(default);
        private readonly ReactiveProperty<CardSpriteSet> currentSpriteSet = new(null);
        private CardSpriteSetReference currentReference;

        // Serializes ApplySkinAsync so rapid taps run FIFO instead of racing — without this,
        // out-of-order completions can leave currentSkinId/Reference/SpriteSet mismatched and
        // release the wrong AssetReference.
        private readonly SemaphoreSlim applyGate = new SemaphoreSlim(1, 1);

        public SkinService(ISkinCatalog catalog, ISkinAssetGateway gateway, ISkinPreferenceStore preferences)
        {
            this.catalog = catalog;
            this.gateway = gateway;
            this.preferences = preferences;
        }

        public IReadOnlyList<SkinInfo> AvailableSkins => catalog.Skins;
        public ReadOnlyReactiveProperty<SkinId> CurrentSkinId => currentSkinId;
        public ReadOnlyReactiveProperty<CardSpriteSet> CurrentSpriteSet => currentSpriteSet;

        public async UniTask InitializeAsync()
        {
            await ApplySkinAsync(ResolveInitialSkinId(), persist: false);
        }

        public async UniTask SelectSkinAsync(SkinId id)
        {
            if (id.Equals(currentSkinId.Value) && currentSpriteSet.Value != null)
                return;
            await ApplySkinAsync(id, persist: true);
        }

        private SkinId ResolveInitialSkinId()
        {
            if (!preferences.TryLoad(out var saved))
                return DefaultSkinId;
            if (catalog.Contains(saved))
                return saved;

            // Persisted user state is a system boundary: a previously-selected skin may have been
            // removed from the catalog. Validating it here and defaulting is boundary input handling,
            // NOT a failover that masks a code bug.
            Debug.LogWarning($"[SkinService] Saved skin '{saved}' not in catalog — using default '{DefaultSkinId}'.");
            return DefaultSkinId;
        }

        private async UniTask ApplySkinAsync(SkinId id, bool persist)
        {
            await applyGate.WaitAsync();
            try
            {
                // Re-check the no-op condition INSIDE the gate: an earlier queued call may have
                // already applied this same id while we were waiting.
                if (persist && id.Equals(currentSkinId.Value) && currentSpriteSet.Value != null)
                    return;

                if (!catalog.TryGet(id, out var entry))
                    throw new KeyNotFoundException($"[SkinService] Skin id '{id}' not found in catalog.");

                // Load the new set BEFORE releasing the old one to avoid a blank frame.
                var newSet = await gateway.LoadAsync(entry.SpriteSetRef);
                if (newSet == null)
                    throw new InvalidOperationException(
                        $"[SkinService] Gateway returned null CardSpriteSet for skin '{id}' — likely a failed/incomplete Addressables load.");

                var previousReference = currentReference;
                currentReference = entry.SpriteSetRef;
                if (previousReference != null && previousReference != entry.SpriteSetRef)
                    gateway.Release(previousReference);

                currentSpriteSet.Value = newSet;
                currentSkinId.Value = id;

                if (persist)
                    preferences.Save(id);
            }
            finally
            {
                // App teardown can dispose applyGate while an apply is mid-await; guard the release
                // so the finally doesn't throw on an already-disposed semaphore.
                try { applyGate.Release(); }
                catch (ObjectDisposedException) { }
            }
        }

        public void Dispose()
        {
            // Release the live Addressables handle so the gateway doesn't keep the last
            // CardSpriteSet loaded across an editor domain reload / scope re-creation.
            if (currentReference != null)
                gateway.Release(currentReference);
            currentSkinId.Dispose();
            currentSpriteSet.Dispose();
            applyGate.Dispose();
        }
    }
}
