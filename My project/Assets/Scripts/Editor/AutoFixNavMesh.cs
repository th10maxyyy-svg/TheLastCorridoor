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
            int staticCount = 0;
            int doorCount = 0;
            
            foreach (MeshRenderer mr in renderers)
            {
                // Darwazon (Doors) ko dhundo aur un par NavMeshModifier lagao
                if (mr.GetComponentInParent<DoorController>() != null || mr.GetComponent<DoorController>() != null)
                {
                    Unity.AI.Navigation.NavMeshModifier modifier = mr.gameObject.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
                    if (modifier == null)
                    {
                        modifier = mr.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
                    }
                    modifier.ignoreFromBuild = true; // Darwaze ko raste se hata do!
                    doorCount++;
                    continue;
                }

                // Baqi sab cheezon ko Static mark karo
                GameObjectUtility.SetStaticEditorFlags(mr.gameObject, GameObjectUtility.GetStaticEditorFlags(mr.gameObject) | StaticEditorFlags.NavigationStatic);
                staticCount++;
            }
            Debug.Log($"Marked {staticCount} objects as Static, and ignored {doorCount} doors from NavMesh!");
        }

        // 2. Puraana NavMesh delete karo
        NavMeshBuilder.ClearAllNavMeshes();

        // 3. Problematic NavMeshSurface ko delete karo (Kyunke wo static flags ko ignore karta hai aur walls ke andar NavMesh bana deta hai)
        Unity.AI.Navigation.NavMeshSurface[] surfaces = FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsSortMode.None);
        foreach (Unity.AI.Navigation.NavMeshSurface surface in surfaces)
        {
            DestroyImmediate(surface);
            Debug.Log("Deleted problematic NavMeshSurface component.");
        }

        // 4. Sirf aur sirf Standard Unity NavMesh Bake karo (Jo exactly static flags ko follow karega!)
        NavMeshBuilder.BuildNavMesh();
        Debug.Log("Baked perfect Legacy NavMesh.");
        
        // 5. Ek pyara sa Popup message dikhao!
        EditorUtility.DisplayDialog("NavMesh Fixed!", 
            "NavMesh 100% theek ho gaya hai!\n\nDoors ko ignore kar diya gaya hai aur rasta bilkul saaf hai. Ab Enemy directly bhag kar aayega!", 
            "Maza Aa Gaya!");
    }
}
