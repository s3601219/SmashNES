using UnityEditor;
using UnityEngine;

class SpriteImport64Post : AssetPostprocessor
{
    const int CellW = 64, CellH = 64;

    void OnPreprocessTexture()
    {
        if (!assetPath.EndsWith(".png")) return;
        if (!assetPath.StartsWith("Assets/Sprites/Character")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Multiple;
        importer.mipmapEnabled       = false;
        importer.filterMode          = FilterMode.Point;
        importer.textureCompression  = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 64;

        // ✅ Set mesh type via TextureImporterSettings (portable across Unity versions)
        var tis = new TextureImporterSettings();
        importer.ReadTextureSettings(tis);
        tis.spriteMeshType = SpriteMeshType.FullRect;   // this is the setting you wanted
        importer.SetTextureSettings(tis);
    }

    void OnPostprocessTexture(Texture2D tex)
    {
        if (!assetPath.EndsWith(".png")) return;
        if (!assetPath.StartsWith("Assets/Sprites/")) return; // <- adjust to your folder

        int cols = tex.width  / CellW;
        int rows = tex.height / CellH;
        if (cols <= 0 || rows <= 0 || tex.width % CellW != 0 || tex.height % CellH != 0)
        {
            Debug.LogWarning($"[{System.IO.Path.GetFileName(assetPath)}] not multiple of 64; skipping auto-slice.");
            return;
        }

        var importer = (TextureImporter)assetImporter;
        var metas = new SpriteMetaData[cols * rows];

        int i = 0;
        for (int y = rows - 1; y >= 0; y--)
        for (int x = 0; x < cols; x++)
        {
            metas[i] = new SpriteMetaData
            {
                name      = $"{System.IO.Path.GetFileNameWithoutExtension(assetPath)}_{i}",
                rect      = new Rect(x * CellW, y * CellH, CellW, CellH),
                alignment = (int)SpriteAlignment.Custom,
                pivot     = new Vector2(0.5f, 0f) // bottom-center
            };
            i++;
        }

        importer.spritesheet = metas;

        // Reimport with the new metas applied
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }
}
