using System.Linq;
using UnityEditor;
using UnityEngine;

public class BatchFixSprites : EditorWindow
{
    static readonly Vector2 TargetPivot = new Vector2(0.5f, 0f); // bottom-center
    const int   TargetPPU    = 64;
    const bool  SetFullRect  = true;
    const bool  PointFilter  = true;
    const bool  Uncompressed = true;

    [MenuItem("Tools/Sprites/Batch Fix (Recursive)")]
    static void Run()
    {
        var roots = Selection.objects
            .Select(AssetDatabase.GetAssetPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();

        if (roots.Length == 0)
        {
            EditorUtility.DisplayDialog("Batch Fix Sprites",
                "Select one or more folders (or textures) in the Project window.", "OK");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Texture2D", roots);
        int changed = 0, skipped = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { skipped++; continue; }

                // Common import settings
                importer.textureType         = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled       = false;
                if (PointFilter)  importer.filterMode = FilterMode.Point;
                if (Uncompressed) importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = TargetPPU;

                // MeshType via TextureImporterSettings (works across versions)
                if (SetFullRect)
                {
                    var tis = new TextureImporterSettings();
                    importer.ReadTextureSettings(tis);
                    tis.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(tis);
                }

                if (importer.spriteImportMode == SpriteImportMode.Multiple)
                {
                    // Use legacy spritesheet API and silence the deprecation *locally*
#pragma warning disable 0618
                    var metas = importer.spritesheet;
#pragma warning restore 0618
                    if (metas != null && metas.Length > 0)
                    {
                        for (int i = 0; i < metas.Length; i++)
                        {
                            metas[i].alignment = (int)SpriteAlignment.Custom;
                            metas[i].pivot     = TargetPivot;
                        }
#pragma warning disable 0618
                        importer.spritesheet = metas;
#pragma warning restore 0618
                    }
                }
                else // Single
                {
                    importer.spriteImportMode = SpriteImportMode.Single; // ensure
                    // Prefer SerializedObject for cross-version safety
                    var so = new SerializedObject(importer);
                    so.FindProperty("m_SpriteAlignment").intValue = (int)SpriteAlignment.Custom;
                    so.FindProperty("m_SpritePivot").vector2Value = TargetPivot;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                importer.SaveAndReimport();
                changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Batch Fix Sprites: updated {changed} texture(s), skipped {skipped}.");
    }
}
