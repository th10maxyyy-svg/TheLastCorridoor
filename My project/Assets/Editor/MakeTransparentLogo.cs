using UnityEditor;
using UnityEngine;
using System.IO;

public class MakeTransparentLogo
{
    [MenuItem("Tools/Make Selected Image Transparent")]
    public static void ProcessLogo()
    {
        if (Selection.activeObject == null || !(Selection.activeObject is Texture2D))
        {
            Debug.LogError("Please select an image in the Project window first!");
            return;
        }

        string inputPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        string extension = Path.GetExtension(inputPath);
        string outputPath = inputPath.Replace(extension, "_transparent.png");

        // Ensure the original texture is readable
        TextureImporter importer = AssetImporter.GetAtPath(inputPath) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(inputPath);
        if (tex == null) return;

        Texture2D newTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        Color[] pixels = tex.GetPixels();
        
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            float maxVal = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            
            // Standard threshold for clean black backgrounds
            float lower = 0.05f; 
            float upper = 0.15f; 
            
            if (maxVal < lower)
                c.a = 0f; 
            else if (maxVal > upper)
                c.a = 1f; 
            else
                c.a = (maxVal - lower) / (upper - lower); 
                
            pixels[i] = c;
        }
        
        newTex.SetPixels(pixels);
        newTex.Apply();

        byte[] pngData = newTex.EncodeToPNG();
        File.WriteAllBytes(outputPath, pngData);
        
        AssetDatabase.Refresh();
        
        TextureImporter newImporter = AssetImporter.GetAtPath(outputPath) as TextureImporter;
        if (newImporter != null)
        {
            newImporter.textureType = TextureImporterType.Sprite;
            newImporter.alphaIsTransparency = true;
            newImporter.SaveAndReimport();
        }
        
        Debug.Log("Magic successful! Created completely transparent version: " + outputPath);
    }
}
