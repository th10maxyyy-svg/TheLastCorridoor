#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ROOM LINKING FIX for the game level (neww). The bug check found rooms whose key existed but whose door was never
/// locked, so every key after the basement was useless and the story could be skipped:
///   Surgery (surgery key), Exam Room (exam room key), Office/ground floor (pharmacy key), Visiting Room (visit room key),
///   Dining Room (dining room key), Classroom (class room key), Head Office (office key),
///   Bathroom inner door (opened by the Bathroom fuse box - no key).
/// It also makes the Head Office safe open with the clock code 0315 (that keypad opened nothing before).
/// Runs once by itself and saves the scene; run again from: The Last Corridor / Fix Room Locks (bug check).
/// Every key is reachable before its door (checked against the objective chain), so nothing can soft-lock.
/// </summary>
[InitializeOnLoad]
public static class LevelLinkFixSetup
{
    private const string PrefKey = "TLC_LevelLinkFix_v1_";
    private const string SceneName = "neww";
    private static bool triedThisSession;

    // door path (under the scene root "Asylum Variant") -> key ("" = opened by a puzzle)
    private static readonly string[,] Doors =
    {
        { "2ndFloor/Surgery/DoorD_V1/DoorD_Right",        "surgery key" },
        { "2ndFloor/Surgery/DoorD_V1/DoorD_Left",         "surgery key" },
        { "2ndFloor/ExamRoom/Door_V1 (7)/Door",           "exam room key" },
        { "1stFloor/OfficeA/Door_V1 (1)/Door",            "pharmacy key" },
        { "2ndFloor/Bedroom/DoorD_V2/DoorD_V2_Right",      "visit room key" },
        { "2ndFloor/Bedroom/DoorD_V2/DoorD_V2_Left",       "visit room key" },
        { "1stFloor/DiningRoom/DoorD_V2 (1)/DoorD_V2_Right", "dining room key" },
        { "1stFloor/DiningRoom/DoorD_V2 (1)/DoorD_V2_Left",  "dining room key" },
        { "2ndFloor/Classroom/Door_V1 (6)/Door",          "class room key" },
        { "2ndFloor/Office/Door_V1 (4)/Door",             "office key" },
        { "2ndFloor/Bathroom/Door_V2 (4)/Door",           "" },
    };

    static LevelLinkFixSetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += TryAuto; return; }
        triedThisSession = true;
        if (Run()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Fix Room Locks (bug check)")]
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
        if (path == null) { Debug.LogWarning("[TLC] Room locks: scene '" + SceneName + "' not found."); return false; }

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        List<string> done = new List<string>(), missing = new List<string>();
        try
        {
            Transform asylum = null;
            foreach (GameObject r in scene.GetRootGameObjects()) if (r.name == "Asylum Variant") { asylum = r.transform; break; }
            if (asylum == null) { Debug.LogWarning("[TLC] Room locks: 'Asylum Variant' not found."); return false; }

            for (int i = 0; i < Doors.GetLength(0); i++)
            {
                Transform t = asylum.Find(Doors[i, 0]);
                DoorController d = t != null ? t.GetComponent<DoorController>() : null;
                if (d == null) { missing.Add(Doors[i, 0]); continue; }
                SerializedObject so = new SerializedObject(d);
                so.FindProperty("isLocked").boolValue = true;
                so.FindProperty("requiredKey").stringValue = Doors[i, 1];
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(d);
                done.Add(Doors[i, 0].Split('/')[1] + (Doors[i, 1].Length > 0 ? " (" + Doors[i, 1] + ")" : " (fuse box)"));
            }

            // Head Office safe: the outer keypad (clock code 0315) opened nothing - now it opens the safe door
            Transform safe = asylum.Find("2ndFloor/Office/safe (1)");
            KeypadController pad = safe != null ? safe.GetComponent<KeypadController>() : null;
            Transform safeDoorT = safe != null ? safe.Find("Cube.001") : null;
            DoorController safeDoor = safeDoorT != null ? safeDoorT.GetComponent<DoorController>() : null;
            if (pad != null && safeDoor != null && pad.onUnlock.GetPersistentEventCount() == 0)
            {
                UnityEventTools.AddVoidPersistentListener(pad.onUnlock, safeDoor.UnlockDoor);
                EditorUtility.SetDirty(pad);
                done.Add("Head Office safe opens with 0315");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        Debug.Log("[TLC] Room locks fixed (" + done.Count + "): " + string.Join(", ", done) +
                  (missing.Count > 0 ? "   NOT FOUND: " + string.Join(", ", missing) : "") + ". Scene saved.");
        return missing.Count == 0;
    }
}
#endif
