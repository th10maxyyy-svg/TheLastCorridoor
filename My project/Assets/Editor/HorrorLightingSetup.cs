using UnityEngine;
using UnityEditor;

public class HorrorLightingSetup
{
    [MenuItem("Tools/Make Game Pitch Black")]
    public static void MakePitchBlack()
    {
        // 1. Ambient Light ko bilkul Black kar do
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;

        // 2. Skybox (Aasman ki roshni) hata do
        RenderSettings.skybox = null;

        // 3. Directional Light (Sooraj ki roshni) ko band kar do
        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light l in allLights)
        {
            if (l.type == LightType.Directional)
            {
                l.enabled = false;
                Debug.Log("Disabled Directional Light: " + l.name);
            }
        }

        Debug.Log("Game is now PITCH BLACK! Player is blind without a headlamp.");
    }
}
