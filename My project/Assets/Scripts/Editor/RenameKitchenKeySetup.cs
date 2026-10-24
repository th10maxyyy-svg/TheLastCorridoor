#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The Dining Room key was called "stock room key" but it opens the KITCHEN (ground floor, the gas room) - players went
/// to the Stock Room with it and found nothing. It is now the "kitchen key" (the key and the Kitchen door).
/// The Stock Room itself stays open (it only holds a gate-code note). Runs once by itself and saves the scene;
/// run again from: The Last Corridor / Rename Kitchen Key.
/// </summary>
[InitializeOnLoad]
public static class RenameKitchenKeySetup
{
    private const string PrefKey = "TLC_RenameKitchenKey_v1_";
    private const string SceneName = "neww";
    private static bool triedThisSession;

    static RenameKitchenKeySetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += TryAuto; return; }
        triedThisSession = true;
        if (Run()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Rename Kitchen Key")]
    private static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[TLC] Stop Play mode first."); return; }
        Run();
    }

    private static string PathOf(Transform t) { string p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }

    private static bool Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        string path = null;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.StartsWith("Assets/") && System.IO.Path.GetFileNameWithoutExtension(p) == SceneName) { path = p; break; }
        }
        if (path == null) return false;

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        int changed = 0;
        try
        {
            foreach (KeyItem k in Object.FindObjectsByType<KeyItem>(FindObjectsInactive.Include))
            {
                if (k.gameObject.scene != scene) continue;
                string n = (k.GetKeyName() ?? "").Trim().ToLowerInvariant(), p = PathOf(k.transform);
                if (n == "stock room key" && p.Contains("/DiningRoom/")) changed += Set(k, "keyName", "kitchen key");
            }
            foreach (DoorController d in Object.FindObjectsByType<DoorController>(FindObjectsInactive.Include))
            {
                if (d.gameObject.scene != scene) continue;
                string n = (d.RequiredKey ?? "").Trim().ToLowerInvariant(), p = PathOf(d.transform);
                if (n == "stock room key" && p.Contains("/KitchenA/")) changed += Set(d, "requiredKey", "kitchen key");
            }
            if (changed > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            Debug.Log("[TLC] Dining Room key renamed to 'kitchen key' (" + changed + " changes). Scene saved.");
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        return true;
    }

    private static int Set(Object o, string field, string value)
    {
        SerializedObject so = new SerializedObject(o);
        SerializedProperty sp = so.FindProperty(field);
        if (sp == null || sp.stringValue == value) return 0;
        sp.stringValue = value;
        so.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(o);
        return 1;
    }
}
#endif
