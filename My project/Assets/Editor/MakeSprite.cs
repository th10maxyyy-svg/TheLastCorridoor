using UnityEditor;
using UnityEngine;

public class MakeSprite
{
    [MenuItem("Tools/Fix Logo Sprite")]
    public static void FixSprite()
    {
        string path = "Assets/_Recovery/game_title_logo.jpg";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
            Debug.Log("Successfully converted logo to Sprite!");
        }
        else
        {
            Debug.LogError("Could not find the logo at " + path);
        }
    }
}
