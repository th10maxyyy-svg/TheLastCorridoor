using UnityEngine;
using UnityEditor;

public class FixEscapeCameraTool : EditorWindow
{
    [MenuItem("Tools/Auto-Fix Escape Cinematic Camera")]
    public static void FixCameras()
    {
        EscapeCinematicManager ecm = Object.FindAnyObjectByType<EscapeCinematicManager>();
        
        if (ecm == null)
        {
            Debug.LogError("EscapeCinematicManager nahi mila! Make sure scene me 'escape triger' majood ho.");
            return;
        }

        // We know the perfect coordinates from your previous screenshot:
        // X = -0.029, Y = 2.108, Z = 2.457, Y Rotation = -4.316

        // 1. Fix Camera Start Point
        if (ecm.cameraStartPoint != null)
        {
            // Set just outside the door perfectly
            ecm.cameraStartPoint.position = new Vector3(-0.029f, 2.1f, 1.5f); 
            ecm.cameraStartPoint.rotation = Quaternion.Euler(0, -4.3f, 0);
            
            // Destroy accidental camera components
            if (ecm.cameraStartPoint.GetComponent<Camera>()) Object.DestroyImmediate(ecm.cameraStartPoint.GetComponent<Camera>());
            if (ecm.cameraStartPoint.GetComponent<AudioListener>()) Object.DestroyImmediate(ecm.cameraStartPoint.GetComponent<AudioListener>());
        }

        // 2. Fix Camera End Point
        if (ecm.cameraEndPoint != null)
        {
            // Set further back to allow movement
            ecm.cameraEndPoint.position = new Vector3(-0.029f, 2.1f, 3.5f); 
            ecm.cameraEndPoint.rotation = Quaternion.Euler(0, -4.3f, 0);
            
            // Destroy accidental camera components
            if (ecm.cameraEndPoint.GetComponent<Camera>()) Object.DestroyImmediate(ecm.cameraEndPoint.GetComponent<Camera>());
            if (ecm.cameraEndPoint.GetComponent<AudioListener>()) Object.DestroyImmediate(ecm.cameraEndPoint.GetComponent<AudioListener>());
        }

        Debug.Log("<color=green><b>SUCCESS!</b> Escape Cinematic Cameras have been perfectly aligned and fixed!</color>");
    }
}
