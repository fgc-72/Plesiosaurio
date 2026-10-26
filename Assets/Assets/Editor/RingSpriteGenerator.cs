using System.IO;
using UnityEditor;
using UnityEngine;

public static class RingSpriteGenerator
{
    [MenuItem("Tools/Generate Ring Sprite")]
    public static void Generate()
    {
        const int size = 512;
        const float innerRatio = 0.3f; // más alto = anillo más delgado (0.85 fino, 0.5 grueso)

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) / 2f;
        float outer = size / 2f - 1f;
        float inner = outer * innerRatio;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float aOuter = Mathf.Clamp01(outer - d + 0.5f); // borde exterior suavizado
                float aInner = Mathf.Clamp01(d - inner + 0.5f); // borde interior suavizado
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, aOuter * aInner));
            }
        }
        tex.Apply();

        string path = "Assets/Ring.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
}