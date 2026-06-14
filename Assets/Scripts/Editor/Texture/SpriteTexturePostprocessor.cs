using System;
using UnityEditor;

namespace Editor.Texture
{
    /// <summary>
    /// On first import, forces textures under Assets/Sprites/ to Uncompressed
    /// so SpriteAtlas packs from lossless source. Atlas output stays compressed.
    /// Only applies when import settings are missing (fresh import) — manual
    /// changes on already-imported assets are preserved.
    /// </summary>
    public class SpriteTexturePostprocessor : AssetPostprocessor
    {
        private const string SpritesRoot = "Assets/Sprites/";

        private void OnPreprocessTexture()
        {
            if (!assetImporter.importSettingsMissing) return;
            if (!assetPath.StartsWith(SpritesRoot, StringComparison.Ordinal)) return;

            var importer = (TextureImporter)assetImporter;
            var settings = importer.GetDefaultPlatformTextureSettings();
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
