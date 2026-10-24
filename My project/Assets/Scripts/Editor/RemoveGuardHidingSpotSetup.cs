#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Removes the invisible "guard room hiding spot" in front of the Guard Room door (there is nothing to hide in there -
/// the player could "hide" in the open corridor, and its invisible box was in the way). Runs once by itself and saves
/// the scene; run again from: The Last Corridor / Remove Hiding Spot Outside Guard Room.
/// </summary>
[InitializeOnLoad]
public static class RemoveGuardHidingSpotSetup
{
    private const string PrefKey = "TLC_RemoveGuardHide_v1_";
    private const string SceneName = "neww";
    private const string ObjectName = "guard room hiding spot";
    private static bool triedThisSession;

    static RemoveGuardHidingSpotSetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += TryAuto; return; }
        triedThisSession = true;
        if (Run()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Remove Hiding Spot Outside Guard Room")]
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
        if (path == null) return false;

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        try
        {
            int removed = 0;
            foreach (HidingSpot h in Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include))
            {
                if (h.gameObject.scene != scene || h.gameObject.name.Trim().ToLowerInvariant() != ObjectName) continue;
                // only an invisible spot (no model of its own) - never a real locker / wardrobe
                if (h.GetComponentInChildren<Renderer>(true) != null) continue;
                Undo.DestroyObjectImmediate(h.gameObject);
                removed++;
            }
            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("[TLC] Hiding spot outside the Guard Room removed (" + removed + "). Scene saved.");
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        return true;
    }
}
#endif
