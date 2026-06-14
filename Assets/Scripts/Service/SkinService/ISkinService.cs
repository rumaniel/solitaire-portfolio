using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data.Card;
using Data.Skin;
using Model.Skin;
using R3;

namespace Service.SkinService
{
    public interface ISkinService
    {
        IReadOnlyList<SkinInfo> AvailableSkins { get; }
        ReadOnlyReactiveProperty<SkinId> CurrentSkinId { get; }
        ReadOnlyReactiveProperty<CardSpriteSet> CurrentSpriteSet { get; }

        /// <summary>Restores the saved skin (or default) and loads its sprite set. Call once at app start.</summary>
        UniTask InitializeAsync();

        /// <summary>Switches to the given skin: loads new set, releases previous handle, persists selection.</summary>
        UniTask SelectSkinAsync(SkinId id);
    }
}
