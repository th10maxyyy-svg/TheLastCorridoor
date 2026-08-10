using UnityEngine;
using UnityEditor;

public class AsylumSetupTool : EditorWindow
{
    [MenuItem("The Last Corridoor/Setup Asylum Doors")]
    public static void SetupDoors()
    {
        int doorsConfigured = 0;
        
        // Find all MeshRenderers in the scene, and if their name contains "Door", configure them.
        MeshRenderer[] allMeshes = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        
        foreach (MeshRenderer mesh in allMeshes)
        {
            if (mesh.gameObject.name.Contains("Door") && !mesh.gameObject.name.Contains("Frame"))
            {
                GameObject doorObj = mesh.gameObject;
                
                // IMPORTANT: Turn off Static flag so it doesn't leave a ghost mesh behind
                doorObj.isStatic = false;
                
                // Add BoxCollider if missing
                BoxCollider col = doorObj.GetComponent<BoxCollider>();
                if (col == null)
                {
                    col = doorObj.AddComponent<BoxCollider>();
                    // Adjust collider to fit the mesh bounds better
                    col.center = mesh.localBounds.center;
                    col.size = mesh.localBounds.size;
                    Debug.Log($"Added BoxCollider to {doorObj.name}");
                }
                
                // Add DoorController if missing
                DoorController doorCtrl = doorObj.GetComponent<DoorController>();
                if (doorCtrl == null)
                {
                    doorCtrl = doorObj.AddComponent<DoorController>();
                    Debug.Log($"Added DoorController to {doorObj.name}");
                }
                
                doorsConfigured++;
            }
        }
        
        EditorUtility.DisplayDialog("Setup Complete", $"Successfully configured and fixed Static flags for {doorsConfigured} doors in the scene!", "Awesome!");
    }
}
