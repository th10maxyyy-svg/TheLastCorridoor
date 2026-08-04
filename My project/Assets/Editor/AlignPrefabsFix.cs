using UnityEngine;
using UnityEditor;

public class AlignPrefabsFix : MonoBehaviour
{
    [MenuItem("Tools/2-ALIGN CORRIDORS (RUN THIS)")]
    public static void AlignCorridors()
    {
        // Get the actual prefab used by the generator
        CorridorGenerator generator = FindAnyObjectByType<CorridorGenerator>();
        if (generator == null)
        {
            EditorUtility.DisplayDialog("Error", "Corridor Generator nahi mila!", "OK");
            return;
        }

        // Reflection to get the private corridorPrefabs array
        var field = typeof(CorridorGenerator).GetField("corridorPrefabs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        GameObject[] prefabs = (GameObject[])field.GetValue(generator);

        if (prefabs == null || prefabs.Length == 0 || prefabs[0] == null)
        {
            EditorUtility.DisplayDialog("Error", "Generator me koi prefab nahi hai!", "OK");
            return;
        }

        GameObject correctPrefab = prefabs[0];

        // Ensure the prefab itself has correct colliders
        FixCorridorPrefab(correctPrefab);
        PrefabUtility.SavePrefabAsset(correctPrefab);

        // Find Start Area
        GameObject root = GameObject.Find("start area");
        if (root == null)
        {
            EditorUtility.DisplayDialog("Error", "Start Area nahi mila. Pehle 1-CLICK ULTIMATE FIX chalayen.", "OK");
            return;
        }

        // Replace room1 and room2
        Transform room1 = root.transform.Find("room1");
        Transform room2 = root.transform.Find("room2");
        if (room1 != null) Undo.DestroyObjectImmediate(room1.gameObject);
        if (room2 != null) Undo.DestroyObjectImmediate(room2.gameObject);

        GameObject newRoom1 = (GameObject)PrefabUtility.InstantiatePrefab(correctPrefab);
        newRoom1.transform.position = new Vector3(0, 0, 0);
        newRoom1.transform.parent = root.transform;
        newRoom1.name = "room1";

        GameObject newRoom2 = (GameObject)PrefabUtility.InstantiatePrefab(correctPrefab);
        newRoom2.transform.position = new Vector3(0, 0, 10);
        newRoom2.transform.parent = root.transform;
        newRoom2.name = "room2";

        // Rebake NavMesh
        var surface = root.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
        if (surface != null) surface.BuildNavMesh();

        EditorUtility.DisplayDialog("Done!", "Kamray ab aapas mein theek se jur gaye hain! Dono same design ke hain.", "OK");
    }

    private static void FixCorridorPrefab(GameObject prefab)
    {
        foreach (Transform child in prefab.transform)
        {
            if (child.name.ToLower().Contains("floor") || child.name.ToLower().Contains("ceiling"))
            {
                MeshCollider mc = child.GetComponent<MeshCollider>();
                if (mc != null) DestroyImmediate(mc, true);
                
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
