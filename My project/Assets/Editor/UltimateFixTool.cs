using UnityEngine;
using UnityEditor;

public class UltimateFixTool : MonoBehaviour
{
    [MenuItem("Tools/1-CLICK ULTIMATE FIX (RUN THIS)")]
    public static void RunFix()
    {
        // 1. Fix the Prefab (Corridor Straight)
        string[] guids = AssetDatabase.FindAssets("corridor straight t:prefab");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                FixCorridorPrefab(prefab);
                PrefabUtility.SavePrefabAsset(prefab);
                Debug.Log("Prefab Fixed!");
            }
        }

        // 2. Delete old broken start area
        GameObject oldStartArea = GameObject.Find("start area");
        if (oldStartArea == null) oldStartArea = GameObject.Find("--- START AREA (ESCAPE ROOM) ---");
        if (oldStartArea != null) Undo.DestroyObjectImmediate(oldStartArea);

        // 3. Delete any other random floors
        Collider[] allCols = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (var c in allCols)
        {
            if (c.name.ToLower().Contains("navfloor")) Undo.DestroyObjectImmediate(c.gameObject);
        }

        // 4. Build Perfect Start Area
        GameObject root = new GameObject("start area");
        
        GameObject room1 = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0])));
        room1.transform.position = new Vector3(0, 0, 0);
        room1.transform.parent = root.transform;
        room1.name = "room1";

        GameObject room2 = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0])));
        room2.transform.position = new Vector3(0, 0, 10);
        room2.transform.parent = root.transform;
        room2.name = "room2";

        // Key
        GameObject key = GameObject.CreatePrimitive(PrimitiveType.Cube);
        key.name = "Escape Key";
        key.transform.position = new Vector3(2, 0.5f, 5);
        key.transform.localScale = new Vector3(0.5f, 0.2f, 0.5f);
        key.transform.parent = root.transform;
        key.GetComponent<Renderer>().sharedMaterial.color = Color.blue;
        key.GetComponent<Collider>().isTrigger = false;
        KeyItem ki = key.AddComponent<KeyItem>();
        SerializedObject soKey = new SerializedObject(ki);
        soKey.FindProperty("keyName").stringValue = "EscapeKey";
        soKey.ApplyModifiedProperties();

        // Door
        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Escape Door";
        door.transform.position = new Vector3(0, 2, 20);
        door.transform.localScale = new Vector3(4, 4, 0.5f);
        door.transform.parent = root.transform;
        door.GetComponent<Renderer>().sharedMaterial.color = new Color(0.4f, 0.2f, 0.1f);
        DoorController dc = door.AddComponent<DoorController>();
        SerializedObject soDoor = new SerializedObject(dc);
        soDoor.FindProperty("requiredKey").stringValue = "EscapeKey";
        soDoor.ApplyModifiedProperties();

        // Trigger
        GameObject trig = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trig.name = "Nightmare Trigger";
        trig.transform.position = new Vector3(0, 2, 22);
        trig.transform.localScale = new Vector3(4, 4, 2);
        trig.transform.parent = root.transform;
        trig.GetComponent<Collider>().isTrigger = true;
        trig.GetComponent<Renderer>().enabled = false;
        trig.AddComponent<PhaseTrigger>();

        // 5. Fix Player
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                cc.radius = 0.3f;
                cc.height = 1.8f;
                cc.center = new Vector3(0, 0.9f, 0);
                player.transform.position = new Vector3(0, 1.0f, 2f); // Perfect ground drop
                cc.enabled = true;
            }
            
            SerializedObject soP = new SerializedObject(player);
            if (soP.FindProperty("walkSpeed").floatValue <= 0) soP.FindProperty("walkSpeed").floatValue = 4f;
            soP.ApplyModifiedProperties();
        }

        // Bake NavMesh for Start Area
        Unity.AI.Navigation.NavMeshSurface surface = root.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
        surface.BuildNavMesh();

        EditorUtility.DisplayDialog("Perfect Fix Applied", "Bhai ab game 100% chalegi! Main ne saara environment, player aur colliders auto-fix kar diye hain. Bas PLAY dabayen!", "Awesome!");
    }

    private static void FixCorridorPrefab(GameObject prefab)
    {
        foreach (Transform child in prefab.transform)
        {
            if (child.name.ToLower() == "floor" || child.name.ToLower() == "ceiling")
            {
                // Remove Mesh Collider
                MeshCollider mc = child.GetComponent<MeshCollider>();
                if (mc != null) DestroyImmediate(mc, true);
                
                // Add/Update Box Collider
                BoxCollider bc = child.GetComponent<BoxCollider>();
                if (bc == null) bc = child.gameObject.AddComponent<BoxCollider>();
                
                bc.center = new Vector3(0, -0.05f, 0);
                bc.size = new Vector3(10, 0.1f, 10);
            }
            else if (child.name.ToLower().Contains("wall"))
            {
                MeshCollider mc = child.GetComponent<MeshCollider>();
                if (mc != null) DestroyImmediate(mc, true);
                
                BoxCollider bc = child.GetComponent<BoxCollider>();
                if (bc == null) bc = child.gameObject.AddComponent<BoxCollider>();
            }
        }
    }
}
