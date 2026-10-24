#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SAFE CODE HINTS (both safes open with 4132, but nothing in the game told the player the digits):
///   - the dosage note in the ground-floor Office (colours only - the player counts the bottles) also says the Head Office safe uses it
///   - a new note next to the Head Office safe (the pendulum is inside it) says the Director uses that same dosage code
/// Runs once by itself and saves the scene; run again from: The Last Corridor / Setup Safe Code Hints.
/// </summary>
[InitializeOnLoad]
public static class SafeCodeHintsSetup
{
    private const string PrefKey = "TLC_SafeCodeHints_v3_";
    private const string SceneName = "neww";
    private const string DosageText = "WARNING! The required dosage for the patient must be exactly: Red, Green, Blue, Yellow.\nThe Director uses the same dosage for his safes - this one and the one in the Head Office upstairs.";
    private const string HeadOfficeText = "Note to self: the pendulum stays in the safe until the clock is repaired.\nSame code as the office safe downstairs - the patient's dosage, pill by pill.";
    private static bool triedThisSession;

    static SafeCodeHintsSetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += TryAuto; return; }
        triedThisSession = true;
        if (Run()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Setup Safe Code Hints")]
    private static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[TLC] Stop Play mode first."); return; }
        Run();
    }

    private static GameObject FindPath(Scene scene, string path)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
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
            // 1. the dosage note downstairs: colours WITH their numbers
            GameObject dosage = FindPath(scene, "Asylum Variant/1stFloor/OfficeA/Paper (7)");
            ReadableNote dn = dosage != null ? dosage.GetComponent<ReadableNote>() : null;
            if (dn == null) { Debug.LogWarning("[TLC] Dosage note (OfficeA/Paper (7)) not found."); return false; }
            Undo.RecordObject(dn, "Dosage note"); dn.noteText = DosageText; EditorUtility.SetDirty(dn);

            // 2. a note beside the Head Office safe
            GameObject safe = FindPath(scene, "Asylum Variant/2ndFloor/Office/safe (1)");
            if (safe != null && FindPath(scene, "Asylum Variant/2ndFloor/Office/SafeCodeNote") == null)
            {
                Bounds sb = new Bounds(safe.transform.position, Vector3.zero); bool any = false;
                foreach (Renderer r in safe.GetComponentsInChildren<Renderer>()) { if (!any) { sb = r.bounds; any = true; } else sb.Encapsulate(r.bounds); }
                GameObject note = Object.Instantiate(dosage, safe.transform.parent);
                note.name = "SafeCodeNote";
                note.GetComponent<ReadableNote>().noteText = HeadOfficeText;
                Physics.SyncTransforms();
                // on the same table, beside the safe (try a few sides, take the first free table spot)
                Vector3[] dirs = { safe.transform.right, -safe.transform.right, safe.transform.forward, -safe.transform.forward };
                Vector3 best = sb.center + safe.transform.right * (sb.extents.magnitude + 0.15f);
                float bestY = sb.min.y;
                foreach (Vector3 d0 in dirs)
                {
                    Vector3 d = Vector3.ProjectOnPlane(d0, Vector3.up).normalized;
                    Vector3 p = sb.center + d * (Mathf.Max(sb.extents.x, sb.extents.z) + 0.16f);
                    if (Physics.Raycast(new Vector3(p.x, sb.max.y + 0.3f, p.z), Vector3.down, out RaycastHit h, 1f, ~0, QueryTriggerInteraction.Ignore)
                        && Mathf.Abs(h.point.y - sb.min.y) < 0.06f) { best = p; bestY = h.point.y; break; }
                }
                note.transform.position = new Vector3(best.x, bestY + 0.003f, best.z);
                note.transform.rotation = Quaternion.AngleAxis(25f, Vector3.up) * dosage.transform.rotation;   // lies flat like the original
                EditorUtility.SetDirty(note);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TLC] Safe code hints set: dosage note shows 4-1-3-2, and a note beside the Head Office safe. Scene saved.");
            ok = true;
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        return ok;
    }
}
#endif
