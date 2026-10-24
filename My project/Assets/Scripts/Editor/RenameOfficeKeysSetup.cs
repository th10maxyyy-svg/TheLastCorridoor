#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Clear key names:
///   Exam Room cabinet key  "pharmacy key" -> "office key"       (opens the ground-floor Office)
///   Classroom key          "office key"   -> "head office key"  (opens the Head Office upstairs)
/// Renames the keys AND the doors that need them, so nothing breaks. Runs once by itself and saves the scene;
/// run again from: The Last Corridor / Rename Office Keys.
/// </summary>
[InitializeOnLoad]
public static class RenameOfficeKeysSetup
{
    private const string PrefKey = "TLC_RenameOfficeKeys_v1_";
    private const string SceneName = "neww";
    private static bool triedThisSession;

    static RenameOfficeKeysSetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += TryAuto; return; }
        triedThisSession = true;
        if (Run()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Rename Office Keys")]
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
            // keys (the Classroom one first, so the two names never clash)
            foreach (KeyItem k in Object.FindObjectsByType<KeyItem>(FindObjectsInactive.Include))
            {
                if (k.gameObject.scene != scene) continue;
                string n = (k.GetKeyName() ?? "").Trim().ToLowerInvariant(), p = PathOf(k.transform);
                if (n == "office key" && p.Contains("/Classroom/")) changed += Set(k, "keyName", "head office key");
            }
            foreach (KeyItem k in Object.FindObjectsByType<KeyItem>(FindObjectsInactive.Include))
            {
                if (k.gameObject.scene != scene) continue;
                if ((k.GetKeyName() ?? "").Trim().ToLowerInvariant() == "pharmacy key") changed += Set(k, "keyName", "office key");
            }
            // doors
            foreach (DoorController d in Object.FindObjectsByType<DoorController>(FindObjectsInactive.Include))
            {
                if (d.gameObject.scene != scene) continue;
                string n = (d.RequiredKey ?? "").Trim().ToLowerInvariant(), p = PathOf(d.transform);
                if (n == "office key" && p.Contains("/2ndFloor/Office/")) changed += Set(d, "requiredKey", "head office key");
                else if (n == "pharmacy key") changed += Set(d, "requiredKey", "office key");
            }
            if (changed > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            Debug.Log("[TLC] Office keys renamed (" + changed + " changes): Exam Room cabinet key = 'office key' (ground-floor Office), Classroom key = 'head office key'. Scene saved.");
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
