using UnityEngine;
using UnityEditor;

public class CreateFuseBoxTool : EditorWindow
{
    [MenuItem("Tools/Create Auto Fuse Box")]
    public static void CreateBox()
    {
        // 1. Box Banayen
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Fuse Box (Auto)";
        box.transform.localScale = new Vector3(0.3f, 0.4f, 0.1f);
        
        // 2. Collider ko non-trigger rakhein taake Raycast hit ho
        BoxCollider col = box.GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = false;

        // 3. Script Lagayen
        FuseBoxPuzzle puzzleScript = box.AddComponent<FuseBoxPuzzle>();

        // Auto-select in editor
        Selection.activeGameObject = box;
        
        Debug.Log("Perfect Fuse Box created automatically!");
    }
}
