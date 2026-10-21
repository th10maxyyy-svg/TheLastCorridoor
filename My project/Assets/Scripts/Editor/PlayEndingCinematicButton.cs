#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// TEST BUTTON for the "Consumed" ending cinematic:
///  - menu  The Last Corridor / Play Ending Cinematic   (Ctrl+Shift+E)
///  - and a "Play Cinematic" button on the ConsumedEndingCinematic object in the Inspector.
/// Outside Play mode it opens the level, starts Play mode directly in it (not from the Intro) and runs the cinematic
/// after the level has loaded. In Play mode inside the level it starts at once.
/// </summary>
[InitializeOnLoad]
public static class PlayEndingCinematicButton
{
    private const string Pending = "TLC_PlayEndingPending";
    private const string OldStart = "TLC_PlayEndingOldStartScene";
    private const string Level = "neww";

    static PlayEndingCinematicButton()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    [MenuItem("The Last Corridor/Play Ending Cinematic %#e")]
    public static void Play()
    {
        if (EditorApplication.isPlaying)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Level) ConsumedEndingCinematic.PlayNow();
            else Debug.LogWarning("[TLC] The ending cinematic plays in the game level (" + Level + "). Stop Play mode and press the button again.");
            return;
        }

        string path = null;
        foreach (string guid in AssetDatabase.FindAssets(Level + " t:Scene"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == Level) { path = p; break; }
        }
        if (path == null) { Debug.LogWarning("[TLC] Level scene '" + Level + "' not found."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // start Play mode directly in the level for this one run (the normal start scene is put back afterwards)
        SessionState.SetString(OldStart, EditorSceneManager.playModeStartScene != null ? AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) : "");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
        {
            SessionState.SetBool(Pending, false);
            double at = EditorApplication.timeSinceStartup + 2.5;   // let the level load and place everything
            EditorApplication.CallbackFunction wait = null;
            wait = () =>
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= wait; return; }
                if (EditorApplication.timeSinceStartup < at) return;
                EditorApplication.update -= wait;
                ConsumedEndingCinematic.PlayNow();
            };
            EditorApplication.update += wait;
        }
        else if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetString(OldStart, null) != null)
        {
            string old = SessionState.GetString(OldStart, "");
            SessionState.EraseString(OldStart);
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(old) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(old);
        }
    }
}

[CustomEditor(typeof(ConsumedEndingCinematic))]
public class ConsumedEndingCinematicEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8);
        GUI.backgroundColor = new Color(0.85f, 0.35f, 0.25f);
        if (GUILayout.Button("▶  Play Cinematic", GUILayout.Height(32))) PlayEndingCinematicButton.Play();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.HelpBox("Plays the 'Consumed' ending cinematic directly (Ctrl+Shift+E). Outside Play mode it starts the level and plays it after 2.5 s.", MessageType.None);
    }
}
#endif
