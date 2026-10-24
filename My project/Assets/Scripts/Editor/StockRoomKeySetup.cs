#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// STOCK ROOM KEY (story fix):
///   - the Stock Room (ground floor, where the body bags with the Bathroom fuse are) is LOCKED and needs the "stock room key"
///   - the stock room key is the reward of the grandfather clock in the Head Office: put the pendulum in, set 3:15,
///     the glass breaks and the key falls on the floor in front of the clock (it replaces the medkit reward)
///   - (an earlier version put the key in the basement with a note in the Kitchen - both are removed again)
/// Runs once by itself and saves the scene; run again from: The Last Corridor / Setup Stock Room Key.
/// </summary>
[InitializeOnLoad]
public static class StockRoomKeySetup
{
    private const string PrefKey = "TLC_StockRoomKey_v2_";
    private const string SceneName = "neww";
    private const string NoteText = "Somebody took the stock room key and hid it down in the basement - Seclusion Room A, right next to the skull. Nobody wants to go down there any more.";
    private static bool triedThisSession;

    static StockRoomKeySetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += TryAuto; return; }
        triedThisSession = true;
        if (Run()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Setup Stock Room Key")]
    private static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[TLC] Stop Play mode first."); return; }
        Run();
    }

    private static string PathOf(Transform t) { string p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }

    private static GameObject FindPath(Scene scene, string path)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (path == root.name) return root.gameObject;
            if (!path.StartsWith(root.name + "/")) continue;
            Transform t = root.transform.Find(path.Substring(root.name.Length + 1));
            if (t != null) return t.gameObject;
        }
        return null;
    }

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
        bool ok = false;
        try
        {
            // 1. the Stock Room door: locked, needs the stock room key
            GameObject doorGo = FindPath(scene, "Asylum Variant/1stFloor/Stock/Door_V1 (8)/Door");
            DoorController door = doorGo != null ? doorGo.GetComponent<DoorController>() : null;
            if (door == null) { Debug.LogWarning("[TLC] Stock Room door not found."); return false; }
            SerializedObject so = new SerializedObject(door);
            so.FindProperty("isLocked").boolValue = true;
            so.FindProperty("requiredKey").stringValue = "stock room key";
            so.ApplyModifiedProperties();

            // 2. the key: the old (unused) stock room key, moved to the basement next to the skeleton
            KeyItem key = null;
            foreach (KeyItem k in Object.FindObjectsByType<KeyItem>(FindObjectsInactive.Include))
            {
                if (k.gameObject.scene != scene) continue;
                if ((k.GetKeyName() ?? "").Trim().ToLowerInvariant() == "stock room key" && !PathOf(k.transform).Contains("/DiningRoom/")) { key = k; break; }
            }
            if (key == null)
            {
                // no spare one: copy the kitchen key and rename the copy
                foreach (KeyItem k in Object.FindObjectsByType<KeyItem>(FindObjectsInactive.Include))
                    if (k.gameObject.scene == scene && PathOf(k.transform).Contains("/DiningRoom/")) { key = Object.Instantiate(k.gameObject).GetComponent<KeyItem>(); break; }
                if (key == null) { Debug.LogWarning("[TLC] No key to use as the stock room key."); return false; }
                SerializedObject ks = new SerializedObject(key);
                ks.FindProperty("keyName").stringValue = "stock room key";
                SerializedProperty ev = ks.FindProperty("onPickUpEvent.m_PersistentCalls.m_Calls");
                if (ev != null) ev.ClearArray();   // the copy must not start the Dining Room ambush
                ks.ApplyModifiedProperties();
            }
            // the key becomes the grandfather clock's reward (instead of the medkit)
            GrandfatherClockPuzzle clock = Object.FindAnyObjectByType<GrandfatherClockPuzzle>(FindObjectsInactive.Include);
            if (clock == null) { Debug.LogWarning("[TLC] Grandfather clock not found."); return false; }
            SerializedObject cs = new SerializedObject(clock);
            GameObject oldReward = cs.FindProperty("stockRoomKey").objectReferenceValue as GameObject;
            cs.FindProperty("stockRoomKey").objectReferenceValue = key.gameObject;
            cs.ApplyModifiedProperties();
            if (oldReward != null && oldReward != key.gameObject && oldReward.GetComponent<Medkit>() != null) Undo.DestroyObjectImmediate(oldReward);
            key.transform.SetParent(clock.transform.parent, true);
            key.name = "stock room key";
            key.transform.position = clock.transform.position + Vector3.ProjectOnPlane(clock.transform.forward, Vector3.up).normalized * 0.5f;
            key.gameObject.SetActive(false);   // appears when the clock is set to 3:15
            EditorUtility.SetDirty(key.gameObject);

            // the basement version's note in the Kitchen is not needed any more
            GameObject oldNote = FindPath(scene, "Asylum Variant/1stFloor/KitchenA/StockKeyNote");
            if (oldNote != null) Undo.DestroyObjectImmediate(oldNote);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TLC] Stock Room locked (stock room key); the key is now the grandfather clock reward in the Head Office. Scene saved.");
            ok = true;
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        return ok;
    }

    private static Bounds Bounds(Transform t)
    {
        Bounds b = new Bounds(t.position, Vector3.zero); bool any = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
#endif
