#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Puts the "ConsumedEndingCinematic" object into the game level (neww) so it is a real object in the Hierarchy
/// (its texts can be edited in the Inspector). Runs by itself whenever the level is open and the object is missing;
/// also in the menu: The Last Corridor / Add Ending Cinematic To Level.
/// </summary>
[InitializeOnLoad]
public static class EndingCinematicSetup
{
    static EndingCinematicSetup()
    {
        EditorApplication.delayCall += () => Add(false);
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += () => Add(false);
    }

    [MenuItem("The Last Corridor/Add Ending Cinematic To Level")]
    private static void Menu() { Add(true); }

    /// <summary>Creates editable shot markers (child objects) at the automatic positions. Move them, save the scene.</summary>
    [MenuItem("The Last Corridor/Create Ending Cinematic Markers")]
    private static void CreateMarkers()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[TLC] Stop Play mode first."); return; }
        Add(false);
        ConsumedEndingCinematic c = Object.FindAnyObjectByType<ConsumedEndingCinematic>(FindObjectsInactive.Include);
        if (c == null) { Debug.LogWarning("[TLC] Open the game level (neww) first."); return; }
        ConsumedEndingCinematic.Shots s = ConsumedEndingCinematic.Compute();
        if (!s.ok) { Debug.LogWarning("[TLC] Could not place the shots automatically: " + s.why); return; }
        Marker(c.transform, "CamCell", s.camCell);
        Marker(c.transform, "EnemyDoor", s.enemyDoor);
        Marker(c.transform, "EnemyStand", s.enemyStand);
        Marker(c.transform, "CamCorridor", s.camCorridor);
        Marker(c.transform, "WalkEnd", s.walkEnd);
        Selection.activeGameObject = c.gameObject;
        EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        Debug.Log("[TLC] Ending cinematic markers created under 'ConsumedEndingCinematic'. Move them in the Scene view, then save the scene.");
    }

    private static void Marker(Transform parent, string n, Vector3 pos)
    {
        Transform t = parent.Find(n);
        if (t == null) { t = new GameObject(n).transform; t.SetParent(parent, false); Undo.RegisterCreatedObjectUndo(t.gameObject, "Ending marker"); }
        Undo.RecordObject(t, "Ending marker");
        t.position = pos;
    }

    private static void Add(bool verbose)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "neww") { if (verbose) Debug.LogWarning("[TLC] Open the game level (neww) first."); return; }
        if (Object.FindAnyObjectByType<ConsumedEndingCinematic>(FindObjectsInactive.Include) != null) { if (verbose) Debug.Log("[TLC] Ending cinematic is already in the level."); return; }

        GameObject go = new GameObject("ConsumedEndingCinematic");
        go.AddComponent<ConsumedEndingCinematic>();
        Undo.RegisterCreatedObjectUndo(go, "Add Ending Cinematic");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TLC] Added 'ConsumedEndingCinematic' to the level (Hierarchy). Its texts can be edited in the Inspector.");
    }
}
#endif
