using UnityEditor;

// Sets up pictures dropped into Assets/Resources/Art the first time Unity sees them, so hand-drawn art
// keeps its see-through background and is not blurred by compression. Later changes made in the Inspector are kept.
class ArtImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Art/")) return;
        var importer = (TextureImporter)assetImporter;
        if (!importer.importSettingsMissing) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        // Lets the game read the picture's pixels, which it needs for painting a picture onto ground tiles.
        importer.isReadable = true;
    }
}

// Music dropped into Assets/Resources/Music is played straight from the file as it goes, instead of being unpacked
// into memory all at once, so a long song does not hold up the game while it loads.
class MusicImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/Music/")) return;
        var importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = UnityEngine.AudioClipLoadType.Streaming;
        importer.defaultSampleSettings = settings;
    }
}
