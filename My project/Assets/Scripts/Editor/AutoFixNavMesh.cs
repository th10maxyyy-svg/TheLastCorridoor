using UnityEngine;
using UnityEditor;
using UnityEditor.AI; // For automatic NavMesh baking

public class AutoFixNavMesh : EditorWindow
{
    // Ye button Unity ke upar "Tools" menu mein add ho jayega!
    [MenuItem("Tools/1-Click NavMesh & Wall Fix")]
    public static void FixNavMeshAndWalls()
    {
        Debug.Log("Auto-Fix started...");

        // 1. Saari deewaron aur zameen ko dhoondo
        GameObject asylum = GameObject.Find("Asylum Variant");
        if (asylum != null)
        {
            MeshRenderer[] renderers = asylum.GetComponentsInChildren<MeshRenderer>();
            int count = 0;
            foreach (MeshRenderer mr in renderers)
            {
                // Darwazon (Doors) ko ignore karein taake wo baad mein khul saken!
                if (mr.GetComponentInParent<DoorController>() != null || mr.GetComponent<DoorController>() != null)
                    continue;

                // Unity ko batayen ke ye object permanently yahan khara hai (Static)
                GameObjectUtility.SetStaticEditorFlags(mr.gameObject, GameObjectUtility.GetStaticEditorFlags(mr.gameObject) | StaticEditorFlags.NavigationStatic);
                count++;
            }
            Debug.Log($"Successfully marked {count} walls and floors as Navigation Static.");
        }
        else
        {
            Debug.LogWarning("Asylum Variant nahi mila, lekin phir bhi koshish kar raha hoon...");
        }

        // 2. Puraana NavMesh delete karo
        NavMeshBuilder.ClearAllNavMeshes();

        // 3. Naya NavMesh Automatically Bake karo!
        NavMeshBuilder.BuildNavMesh();
        
        Debug.Log("SUCCESS! NavMesh has been completely re-baked!");

        // 4. Ek pyara sa Popup message dikhao!
        EditorUtility.DisplayDialog("NavMesh Fixed!", 
            "Saari deewarien theek ho gayi hain aur NavMesh automatically update ho gaya hai!\n\nAb Enemy deewaron ke andar se nai guzrega.", 
            "Maza Aa Gaya!");
    }
}
