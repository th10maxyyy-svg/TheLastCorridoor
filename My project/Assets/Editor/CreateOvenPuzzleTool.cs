using UnityEngine;
using UnityEditor;

public class CreateOvenPuzzleTool : EditorWindow
{
    [MenuItem("Tools/Create Auto Oven Puzzle")]
    public static void CreateOvens()
    {
        // 1. Main Parent
        GameObject mainParent = new GameObject("Burn_Them_Ovens_Puzzle");
        
        // 2. Puzzle Manager
        GameObject managerObj = new GameObject("Oven_Manager");
        managerObj.transform.SetParent(mainParent.transform);
        OvenPuzzleManager managerScript = managerObj.AddComponent<OvenPuzzleManager>();

        // 3. Create 3 Ovens
        OvenDial dial1 = CreateSingleOven(mainParent.transform, "Oven_Left", new Vector3(-1.2f, 0, 0));
        OvenDial dial2 = CreateSingleOven(mainParent.transform, "Oven_Middle", new Vector3(0, 0, 0));
        OvenDial dial3 = CreateSingleOven(mainParent.transform, "Oven_Right", new Vector3(1.2f, 0, 0));

        // 4. Link everything to Manager
        dial1.puzzleManager = managerScript;
        dial2.puzzleManager = managerScript;
        dial3.puzzleManager = managerScript;

        managerScript.oven1 = dial1;
        managerScript.oven2 = dial2;
        managerScript.oven3 = dial3;

        // Set default targets (400, 200, 500)
        managerScript.targetTemp1 = 400;
        managerScript.targetTemp2 = 200;
        managerScript.targetTemp3 = 500;

        // Auto-select
        Selection.activeGameObject = mainParent;
        Debug.Log("AAA Oven Puzzle created automatically!");
    }

    private static OvenDial CreateSingleOven(Transform parent, string name, Vector3 localPos)
    {
        // Oven Body
        GameObject oven = GameObject.CreatePrimitive(PrimitiveType.Cube);
        oven.name = name;
        oven.transform.SetParent(parent);
        oven.transform.localPosition = localPos;
        oven.transform.localScale = new Vector3(1f, 1.5f, 1f); // Industrial oven size
        
        // Give it a dark grey material color if possible
        MeshRenderer mr = oven.GetComponent<MeshRenderer>();
        if (mr != null && mr.sharedMaterial != null)
        {
            Material mat = new Material(mr.sharedMaterial);
            mat.color = new Color(0.2f, 0.2f, 0.2f);
            mr.material = mat;
        }

        // Dial (Sui)
        GameObject dialObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dialObj.name = "Dial_" + name;
        dialObj.transform.SetParent(oven.transform);
        // Position it on the front of the oven
        dialObj.transform.localPosition = new Vector3(0f, 0.3f, -0.55f);
        // Rotate so it faces outward
        dialObj.transform.localRotation = Quaternion.Euler(90, 0, 0);
        dialObj.transform.localScale = new Vector3(0.2f, 0.05f, 0.2f); // Flattened cylinder

        // Add Dial Marker (a small cube on the dial to show where it's pointing)
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Pointer";
        marker.transform.SetParent(dialObj.transform);
        marker.transform.localPosition = new Vector3(0, 1f, 0.3f); // Top edge of the dial
        marker.transform.localScale = new Vector3(0.1f, 1f, 0.4f);
        
        // Remove colliders from marker
        DestroyImmediate(marker.GetComponent<Collider>());

        // Attach Script
        OvenDial dialScript = dialObj.AddComponent<OvenDial>();
        dialScript.dialTransform = dialObj.transform;

        return dialScript;
    }
}
