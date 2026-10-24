#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Finds the tip (blade end) of every pick-up item from its model and stores it on the item (ItemTip),
/// so the scalpel is held and used blade-first. Runs once by itself and saves the scene;
/// run again from: The Last Corridor / Find Item Tips (scalpel blade direction).
/// </summary>
[InitializeOnLoad]
public static class ItemTipSetup
{
    private const string PrefKey = "TLC_ItemTips_v2_";
    private const string SceneName = "neww";
    private static bool triedThisSession;

    static ItemTipSetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += TryAuto; return; }
        triedThisSession = true;
        if (Run()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Find Item Tips (scalpel blade direction)")]
    private static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[TLC] Stop Play mode first."); return; }
        Run();
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
        if (path == null) { Debug.LogWarning("[TLC] Item tips: scene '" + SceneName + "' not found."); return false; }

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        List<string> done = new List<string>();
        try
        {
            System.Collections.Generic.List<GameObject> all = new System.Collections.Generic.List<GameObject>();
            foreach (KeyItem k in Object.FindObjectsByType<KeyItem>(FindObjectsInactive.Include)) all.Add(k.gameObject);
            foreach (TaserGun g in Object.FindObjectsByType<TaserGun>(FindObjectsInactive.Include)) all.Add(g.gameObject);
            foreach (GameObject item in all)
            {
                if (item.scene != scene) continue;
                if (!ItemTip.Compute(item, out Vector3 local, true)) continue;
                ItemTip t = item.GetComponent<ItemTip>();
                if (t == null) t = Undo.AddComponent<ItemTip>(item);
                t.localTip = local; t.measured = true; t.localUp = ItemTip.lastUp;
                EditorUtility.SetDirty(t);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                done.Add(item.name + " " + local);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        Debug.Log("[TLC] Item tips found for " + done.Count + " items: " + string.Join(", ", done) + ". Scene saved.");
        return true;
    }
}
#endif
