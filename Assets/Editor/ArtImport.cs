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
    }
}
