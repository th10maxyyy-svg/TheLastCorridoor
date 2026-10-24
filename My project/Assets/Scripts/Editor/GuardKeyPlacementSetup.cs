#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The Guard Room key was hidden INSIDE a toilet bowl in the Women's Toilets - almost impossible to spot.
/// This puts it on the floor right in front of that toilet (as if it had fallen there), lying flat, where it can be
/// seen from the stall door. Runs once by itself and saves the scene;
/// run again from: The Last Corridor / Move Guard Room Key Out Of The Toilet.
/// </summary>
[InitializeOnLoad]
public static class GuardKeyPlacementSetup
{
    private const string PrefKey = "TLC_GuardKeyFloor_v1_";
    private const string SceneName = "neww";
    private static bool triedThisSession;

    static GuardKeyPlacementSetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += TryAuto; return; }
        triedThisSession = true;
        if (Run()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Move Guard Room Key Out Of The Toilet")]
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
        bool done = false;
        try
        {
            KeyItem key = null;
            foreach (KeyItem k in Object.FindObjectsByType<KeyItem>(FindObjectsInactive.Include))
                if (k.gameObject.scene == scene && (k.GetKeyName() ?? "").ToLowerInvariant().Contains("guard")) { key = k; break; }
            if (key == null) { Debug.LogWarning("[TLC] Guard Room key not found."); return true; }

            Transform kt = key.transform;
            // the toilet it is in, and the stall door in front of it
            Transform toilet = Nearest(kt.position, "toilet", null, 1.2f);
            Transform door = Nearest(kt.position, "toiletdoor", null, 4f);
            if (toilet == null) { Debug.LogWarning("[TLC] No toilet next to the Guard Room key - left where it is."); return true; }

            Vector3 front = door != null ? door.position - toilet.position : -toilet.forward;
            front.y = 0f;
            front = Mathf.Abs(front.x) > Mathf.Abs(front.z) ? new Vector3(Mathf.Sign(front.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(front.z));
            Vector3 spot = new Vector3(toilet.position.x, kt.position.y, toilet.position.z) + front * 0.62f + Vector3.Cross(Vector3.up, front) * 0.12f;

            Undo.RecordObject(kt, "Move Guard Room key");
            kt.position = spot;
            Physics.SyncTransforms();
            float floorY = toilet.position.y;
            if (Physics.Raycast(spot + Vector3.up * 0.6f, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(kt))
                floorY = hit.point.y;
            // lie flat on the floor: lowest point of the model on the floor, a random-looking turn
            kt.rotation = Quaternion.Euler(0f, 37f, 0f) * kt.rotation;
            Bounds b = ModelBounds(kt);
            kt.position += Vector3.up * (floorY + 0.004f - b.min.y);
            EditorUtility.SetDirty(kt);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TLC] Guard Room key moved out of the toilet onto the floor in front of it (" + kt.position + "). Scene saved.");
            done = true;
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        return done;
    }

    private static Transform Nearest(Vector3 p, string nameContains, string notContains, float maxDist)
    {
        Transform best = null; float bd = maxDist;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            string n = t.name.ToLowerInvariant().Replace(" ", "");
            if (!(n == nameContains || n.StartsWith(nameContains + "("))) continue;   // "Toilet", "Toilet (6)" - not the room or the walls
            if (t.GetComponent<Renderer>() == null && t.GetComponentInChildren<Renderer>() == null) continue;
            float d = Vector2.Distance(new Vector2(t.position.x, t.position.z), new Vector2(p.x, p.z));
            if (Mathf.Abs(t.position.y - p.y) < 1.5f && d < bd) { bd = d; best = t; }
        }
        return best;
    }

    private static Bounds ModelBounds(Transform t)
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
