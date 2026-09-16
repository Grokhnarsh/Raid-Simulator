using EmberDepths.Core.Grid;
using UnityEditor;
using UnityEngine;

namespace EmberDepths.EditorTools
{
    /// <summary>
    /// Forces correct pixel-art import settings on everything under the project's
    /// Art folder.
    ///
    /// Every one of these settings is a bug if it is wrong, and all of them
    /// default to wrong for pixel art: bilinear filtering blurs the sprites,
    /// compression eats the palette, mipmaps make distant tiles mush, and a
    /// pixels-per-unit that disagrees with <see cref="IsoGrid"/> makes tiles fail
    /// to tessellate. Automating it means an artist can drop a PNG in and it is
    /// simply right.
    ///
    /// Pivots are deliberately NOT set here — they depend on where the art
    /// actually sits inside its canvas, which needs the pixels. Run
    /// <c>EmberDepths/Art/Fix Pivots</c> for that.
    /// </summary>
    public sealed class PixelArtImportPostprocessor : AssetPostprocessor
    {
        public const string ArtRoot = "Assets/_Project/Art";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;

            var importer = (TextureImporter)assetImporter;

            // Only stamp settings on first import. Re-stamping on every reimport
            // would silently undo an artist's deliberate per-asset override.
            if (!importer.importSettingsMissing) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = IsoGrid.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.spriteBorder = Vector4.zero;

            // FullRect plus zero extrude: tight meshes and extruded borders both
            // shift pixels, which shows up as seams between neighbouring tiles.
            // These two live on TextureImporterSettings rather than on the
            // importer itself, so they need the read/modify/write dance.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);

            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.RGBA32;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.crunchedCompression = false;
            importer.SetPlatformTextureSettings(platform);
        }
    }
}
