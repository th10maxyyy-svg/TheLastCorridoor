using UnityEditor;
using UnityEngine;

public class CreateAdditiveMaterial
{
    [MenuItem("Tools/Create UI Additive Material")]
    public static void CreateMat()
    {
        // Find the built-in UI/Default or Mobile/Particles/Additive shader
        Shader shader = Shader.Find("Mobile/Particles/Additive");
        if (shader == null) shader = Shader.Find("Particles/Additive"); // fallback
        
        if (shader != null)
        {
            Material mat = new Material(shader);
            string path = "Assets/_Recovery/UI_Additive.mat";
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            Debug.Log("Created Additive Material at " + path);
        }
        else
        {
            Debug.LogError("Could not find Additive shader!");
        }
    }
}
