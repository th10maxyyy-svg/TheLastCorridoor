#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Puts the game's whole UI into the Hierarchy as real objects.
///   1. builds the "TLC_UI" Canvas (HUD, objective, pop-ups, pause / save / load / settings / achievements / journal panels,
///      ending summary, keypad, death message, AI panel, cinematic overlay) and saves it as the prefab
///      Assets/Resources/UI/TLC_UI.prefab
///   2. places that prefab in the scenes neww, main menue and Intro (plus an EventSystem if a scene has none) and saves them.
/// Runs once by itself; can be run again from the menu: The Last Corridor / Build UI In Hierarchy
/// (that rebuilds the prefab - changes you made to the prefab by hand are replaced).
/// </summary>
[InitializeOnLoad]
public static class UIHierarchyBuilder
{
    private const string PrefabFolder = "Assets/Resources/UI";
    private const string PrefabPath = PrefabFolder + "/TLC_UI.prefab";
    private const string PrefKey = "TLC_UIHierarchy_v2_";   // v2: rebuild once after TLCCard / TLCRefs / TLCButtonFX moved to their own files
    private static readonly string[] Scenes = { "neww", "main menue", "Intro" };
    private static bool triedThisSession;

    static UIHierarchyBuilder()
    {
        EditorApplication.delayCall += TryAuto;
    }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) { EditorApplication.delayCall += TryAuto; return; }
        triedThisSession = true;
        if (Run()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Build UI In Hierarchy")]
    private static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[TLC] Stop Play mode first."); return; }
        if (File.Exists(PrefabPath) && !EditorUtility.DisplayDialog("The Last Corridor",
                "Rebuild the TLC_UI prefab and put it into the scenes again?\nChanges made to the prefab by hand will be replaced.", "Rebuild", "Cancel"))
            return;
        Run();
    }

    private static bool Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

        GameObject prefab = BuildPrefab();
        if (prefab == null) return false;

        var setup = EditorSceneManager.GetSceneManagerSetup();
        int placed = 0;
        try
        {
            foreach (string name in Scenes)
            {
                string path = FindScene(name);
                if (path == null) { Debug.LogWarning("[TLC] UI: scene '" + name + "' not found - skipped."); continue; }
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                PlaceInScene(scene, prefab);
                placed++;
            }
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        Debug.Log("[TLC] UI is now in the Hierarchy: 'TLC_UI' canvas placed in " + placed + " scene(s). Prefab: " + PrefabPath);
        return placed > 0;
    }

    private static GameObject BuildPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Resources", "UI");
        GameObject root = TLCUIFactory.Build();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
        Object.DestroyImmediate(root);
        if (!ok) { Debug.LogError("[TLC] UI: could not save " + PrefabPath); return null; }
        return prefab;
    }

    private static void PlaceInScene(UnityEngine.SceneManagement.Scene scene, GameObject prefab)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.GetComponent<TLCCanvas>() != null) Object.DestroyImmediate(go);

        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        inst.name = "TLC_UI";
        inst.transform.SetAsLastSibling();

        bool hasEventSystem = false;
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.GetComponentInChildren<EventSystem>(true) != null) { hasEventSystem = true; break; }
        if (!hasEventSystem)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(es, scene);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static string FindScene(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.StartsWith("Assets/") && Path.GetFileNameWithoutExtension(p) == name) return p;
        }
        return null;
    }
}
#endif
