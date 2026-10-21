#if UNITY_EDITOR
#pragma warning disable 0618 // legacy NavMeshBuilder (the project uses the legacy baked NavMesh)
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// WHY THE CLOWN GOT TRAPPED IN ROOMS: the NavMesh was baked with an agent radius of 0.5 m. A doorway of about
/// 1 m then has NO walkable area in it, so every room became a separate "island": once the enemy was inside a room
/// (e.g. after dragging the player away) he could see the player but had no way out.
/// This re-bakes the level NavMesh ONCE with a radius of 0.3 m, so all doorways connect the rooms again, and saves
/// the scene. Can be run again from the menu: The Last Corridor / Rebake NavMesh (connect doorways).
/// </summary>
[InitializeOnLoad]
public static class NavMeshDoorwayFix
{
    private const string LevelScene = "neww";
    private const float NewRadius = 0.3f;
    private const string PrefKey = "TLC_NavMeshDoorways_v1_";
    private static bool triedThisSession;

    static NavMeshDoorwayFix()
    {
        EditorApplication.delayCall += TryAuto;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += TryAuto;
    }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        triedThisSession = true;   // only one automatic attempt per editor session (the menu item can always run it again)
        if (RebakeOpeningLevel()) EditorPrefs.SetBool(key, true);
    }

    /// <summary>Opens the game level if another scene is open (Intro / menu), bakes, saves, and puts the old scenes back.</summary>
    private static bool RebakeOpeningLevel()
    {
        if (EditorSceneManager.GetActiveScene().name == LevelScene) return Rebake();

        string path = null;
        foreach (string guid in AssetDatabase.FindAssets(LevelScene + " t:Scene"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == LevelScene) { path = p; break; }
        }
        if (path == null) { Debug.LogWarning("[TLC] NavMesh fix: level scene '" + LevelScene + "' not found."); return false; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;   // user cancelled - try again next time

        var setup = EditorSceneManager.GetSceneManagerSetup();
        bool ok = false;
        try
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ok = Rebake();
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        return ok;
    }

    [MenuItem("The Last Corridor/Rebake NavMesh (connect doorways)")]
    private static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[TLC] Stop Play mode first."); return; }
        RebakeOpeningLevel();
    }

    private static bool Rebake()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != LevelScene)
        {
            Debug.LogWarning("[TLC] Open the game level (" + LevelScene + ") first, then use The Last Corridor / Rebake NavMesh.");
            return false;
        }

        int before = NavMesh.CalculateTriangulation().vertices.Length;

        // 1. the level's bake settings (legacy NavMesh)
        SerializedObject sceneSettings = new SerializedObject(UnityEditor.AI.NavMeshBuilder.navMeshSettingsObject);
        SerializedProperty r = sceneSettings.FindProperty("m_BuildSettings.agentRadius");
        if (r != null) { r.floatValue = NewRadius; sceneSettings.ApplyModifiedPropertiesWithoutUndo(); }

        // 2. the project's default agent type (Humanoid), which the legacy bake also reads
        Object[] navAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
        if (navAssets != null && navAssets.Length > 0)
        {
            SerializedObject proj = new SerializedObject(navAssets[0]);
            SerializedProperty pr = proj.FindProperty("m_Settings.Array.data[0].agentRadius");
            if (pr != null) { pr.floatValue = NewRadius; proj.ApplyModifiedPropertiesWithoutUndo(); }
        }

        // 3. bake + save
        EditorUtility.DisplayProgressBar("The Last Corridor", "Re-baking the NavMesh so the clown can walk through doorways...", 0.5f);
        try
        {
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        int after = NavMesh.CalculateTriangulation().vertices.Length;
        Debug.Log("[TLC] NavMesh re-baked with agent radius " + NewRadius + " m - doorways now connect the rooms (" + before + " -> " + after + " NavMesh vertices). Scene saved.");
        return true;
    }
}
#endif
