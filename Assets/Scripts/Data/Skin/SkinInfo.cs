using System;
using Model.Skin;
using UnityEngine;

namespace Data.Skin
{
    /// <summary>Lightweight immutable skin metadata for selection UI. The heavy CardSpriteSet is loaded separately.</summary>
    public sealed class SkinInfo : IEquatable<SkinInfo>
    {
        public SkinId Id { get; }
        public string DisplayNameKey { get; }
        public Sprite Thumbnail { get; }

        public SkinInfo(SkinId id, string displayNameKey, Sprite thumbnail)
        {
            Id = id;
            DisplayNameKey = displayNameKey;
            Thumbnail = thumbnail;
        }

        public bool Equals(SkinInfo other)
        {
            if (other is null) return false;
            return Id.Equals(other.Id)
                && string.Equals(DisplayNameKey, other.DisplayNameKey, StringComparison.Ordinal)
                && ReferenceEquals(Thumbnail, other.Thumbnail);
        }

        public override bool Equals(object obj) => obj is SkinInfo other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
    }
}
