using UnityEngine;
using UnityEditor;
using System.IO;

public class FixAcidTexture
{
    [MenuItem("Tools/The Last Corridor/Fix Acid Texture (Make it Flow)")]
    public static void FixAcid()
    {
        int width = 256;
        int height = 256;
        float scale = 15f;

        Texture2D noiseTex = new Texture2D(width, height);
        
        // Asli Tezaab jaisa pattern banate hain (Perlin Noise)
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float xCoord = (float)x / width * scale;
                float yCoord = (float)y / height * scale;
                float sample = Mathf.PerlinNoise(xCoord, yCoord);
                
                // Thora contrast add karte hain
                sample = Mathf.Pow(sample, 1.5f);
                
                noiseTex.SetPixel(x, y, new Color(sample, sample, sample, 1f));
            }
        }
        noiseTex.Apply();

        // Texture ko save karte hain
        if (!AssetDatabase.IsValidFolder("Assets/Textures"))
        {
            AssetDatabase.CreateFolder("Assets", "Textures");
        }
        
        byte[] bytes = noiseTex.EncodeToPNG();
        string path = Application.dataPath + "/Textures/AcidNoise.png";
        File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh(); // Unity ko batayen ke nayi file aayi hai
        
        string assetPath = "Assets/Textures/AcidNoise.png";
        TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(assetPath);
        if (importer != null)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        Texture2D savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

        // Material dhoond kar uspe apply karte hain
        Material acidMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/AcidLiquidMat.mat");
        if (acidMat != null)
        {
            acidMat.SetTexture("_BaseMap", savedTex);
            
            // Isko thora shiny aur transparent set karte hain taake skeleton nazar aaye
            acidMat.SetColor("_BaseColor", new Color(0.1f, 0.9f, 0.1f, 0.7f)); 
            
            EditorUtility.SetDirty(acidMat);
            AssetDatabase.SaveAssets();
            
            Debug.Log("SUCCESS! Tezaab ka texture theek ho gaya hai. Ab game Play karein, ye flow karta nazar aayega!");
        }
        else
        {
            Debug.LogError("Masla: AcidLiquidMat nahi mila.");
        }
    }
}
