using Cysharp.Threading.Tasks;
using Data.Card;
using Data.Skin;

namespace Gateway.Skin
{
    /// <summary>Loads/releases CardSpriteSet assets via Addressables. Isolates Addressables from skin selection logic.</summary>
    public interface ISkinAssetGateway
    {
        /// <summary>Loads the CardSpriteSet for the reference. Throws on Addressables failure (no fallback).</summary>
        UniTask<CardSpriteSet> LoadAsync(CardSpriteSetReference reference);

        /// <summary>Releases the handle previously acquired for the reference. No-op if not currently loaded.</summary>
        void Release(CardSpriteSetReference reference);
    }
}
