using UnityEngine;
using UnityEditor;

public class SetupHorrorEnvironment : MonoBehaviour
{
    [MenuItem("Tools/Setup Extreme Horror Environment")]
    public static void ApplyHorrorSettings()
    {
        // 1. Create a dark horror material
        Material darkWallMat = new Material(Shader.Find("Standard"));
        darkWallMat.color = new Color(0.15f, 0.15f, 0.15f); // Very dark grey
        darkWallMat.SetFloat("_Glossiness", 0.1f); // Not shiny
        
        Material floorMat = new Material(Shader.Find("Standard"));
        floorMat.color = new Color(0.1f, 0.05f, 0.05f); // Dark red/brown tint for floor
        floorMat.SetFloat("_Glossiness", 0.2f);

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        AssetDatabase.CreateAsset(darkWallMat, "Assets/Materials/DarkWall.mat");
        AssetDatabase.CreateAsset(floorMat, "Assets/Materials/DarkFloor.mat");

        // 2. Adjust Lighting (Extreme Haunted)
        RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.07f); // Very dark blue/grey ambient
        RenderSettings.fog = true;
        RenderSettings.fogColor = Color.black;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.05f;

        Light directionalLight = Object.FindAnyObjectByType<Light>();
        if (directionalLight != null && directionalLight.type == LightType.Directional)
        {
            directionalLight.intensity = 0.05f; // Extremely dim moon light
            directionalLight.color = new Color(0.6f, 0.7f, 0.9f);
        }

        Debug.Log("Extreme Horror Atmosphere Applied! Check your lighting and Fog.");
    }
}
