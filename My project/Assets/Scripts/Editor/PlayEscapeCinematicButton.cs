#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// TEST BUTTON for the GRAND ESCAPE (end-game) cinematic:
///  - menu  The Last Corridor / Play Escape Cinematic   (Ctrl+Shift+G)
///  - and a "Play Escape Cinematic" button on the 'escape triger' object (EscapeCinematicManager) in the Inspector.
/// Outside Play mode it opens the level, starts Play mode directly in it (no intro) and plays the cinematic after the
/// level has loaded; in Play mode inside the level it starts at once. It is a TEST RUN: the gate key / code are not
/// needed, nothing is saved, the save file is kept, and Play mode stops after THE END.
/// </summary>
[InitializeOnLoad]
public static class PlayEscapeCinematicButton
{
    private const string Pending = "TLC_PlayEscapePending";
    private const string OldStart = "TLC_PlayEscapeOldStartScene";
    private const string Level = "neww";

    static PlayEscapeCinematicButton()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    [MenuItem("The Last Corridor/Play Escape Cinematic %#g")]
    public static void Play()
    {
        if (EditorApplication.isPlaying)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Level) EscapeCinematicManager.PlayNow();
            else Debug.LogWarning("[TLC] The escape cinematic plays in the game level (" + Level + "). Stop Play mode and press the button again.");
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
                EscapeCinematicManager.PlayNow();
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

[CustomEditor(typeof(EscapeCinematicManager))]
public class EscapeCinematicManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8);
        GUI.backgroundColor = new Color(0.35f, 0.7f, 0.4f);
        if (GUILayout.Button("▶  Play Escape Cinematic", GUILayout.Height(32))) PlayEscapeCinematicButton.Play();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.HelpBox("Test run of the Grand Escape (Ctrl+Shift+G): no gate key / code needed, nothing is saved, Play mode stops after THE END.", MessageType.None);
    }
}
#endif
