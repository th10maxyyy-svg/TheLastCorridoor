#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts the MAIN GATE end-game into the level (neww): the gate needs ONE KEY + an 8-DIGIT CODE.
///   - "Main Gate Lock": trigger box around the exit door with MainGateLock, and its child "Gate Keypad" (KeypadController, code below)
///   - the exit door's DoorController is locked so it cannot simply be opened
///   - "Main Gate Key": hidden in the last room (Bathroom), it appears beside the acid bath once the acid is drained
///   - 8 notes "GateCodeNote_1..8", one digit each, spread over 8 rooms (copies of the Guard Room clue note)
/// Runs once by itself; again from the menu: The Last Corridor / Setup Main Gate (key + code). Running it again rebuilds everything.
/// </summary>
[InitializeOnLoad]
public static class MainGateSetup
{
    private const string LevelScene = "neww";
    private const string PrefKey = "TLC_MainGateKeyCode_v3_";   // v3: bigger, lit code notes
    private const float NoteScale = 2.5f;
    public const string GateCode = "73915284";
    private static bool tried;

    static MainGateSetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || tried) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) { EditorApplication.delayCall += TryAuto; return; }
        tried = true;
        if (RunOpeningLevel()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Setup Main Gate (key + code)")]
    private static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[TLC] Stop Play mode first."); return; }
        RunOpeningLevel();
    }

    private static bool RunOpeningLevel()
    {
        if (EditorSceneManager.GetActiveScene().name == LevelScene) return Build(EditorSceneManager.GetActiveScene());
        string path = null;
        foreach (string guid in AssetDatabase.FindAssets(LevelScene + " t:Scene"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == LevelScene) { path = p; break; }
        }
        if (path == null) { Debug.LogWarning("[TLC] Main gate: level scene not found."); return false; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        bool ok = false;
        try { ok = Build(EditorSceneManager.OpenScene(path, OpenSceneMode.Single)); }
        finally { if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        return ok;
    }

    // ------------------------------------------------------------------ building
    private static bool Build(Scene scene)
    {
        EscapeCinematicManager esc = Object.FindAnyObjectByType<EscapeCinematicManager>(FindObjectsInactive.Include);
        if (esc == null) { Debug.LogWarning("[TLC] Main gate: no EscapeCinematicManager (escape triger) in the level."); return false; }

        // the exit door = nearest DoorController to the escape trigger
        DoorController gate = null; float best = 8f;
        foreach (DoorController d in Object.FindObjectsByType<DoorController>(FindObjectsInactive.Include))
        {
            float dist = Vector3.Distance(Flat(d.transform.position), Flat(esc.transform.position));
            if (dist < best) { best = dist; gate = d; }
        }
        if (gate == null) { Debug.LogWarning("[TLC] Main gate: no door found near the escape trigger."); return false; }

        GameObject template = FindByPath(scene, "Asylum Variant/2ndFloor/ExamRoom/SheetRack/SheetRackCase_12/pharmacy key");
        if (template == null || template.GetComponent<KeyItem>() == null) { Debug.LogWarning("[TLC] Main gate: 'pharmacy key' (used as the key model) not found."); return false; }
        GameObject noteTemplate = FindByPath(scene, "Asylum Variant/1stFloor/GuardRoom/clue");
        if (noteTemplate == null || noteTemplate.GetComponent<ReadableNote>() == null) { Debug.LogWarning("[TLC] Main gate: the Guard Room 'clue' note (used as the note model) not found."); return false; }

        // remove an older setup (also the old 5-keys version)
        foreach (GameObject r in scene.GetRootGameObjects()) if (r.name == "Main Gate Lock") Object.DestroyImmediate(r);
        foreach (GateKeyReveal g in Object.FindObjectsByType<GateKeyReveal>(FindObjectsInactive.Include)) Object.DestroyImmediate(g.gameObject);
        foreach (ReadableNote n in Object.FindObjectsByType<ReadableNote>(FindObjectsInactive.Include))
            if (n != null && n.gameObject.name.StartsWith("GateCodeNote_")) Object.DestroyImmediate(n.gameObject);

        // 1. the lock: a trigger box around the door, so looking at the door shows the gate prompt
        Bounds b = BoundsOf(gate.gameObject);
        GameObject lockGo = new GameObject("Main Gate Lock");
        SceneManager.MoveGameObjectToScene(lockGo, scene);
        lockGo.transform.position = b.center;
        BoxCollider bc = lockGo.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = b.size + new Vector3(0.7f, 0.2f, 0.7f);
        MainGateLock gl = lockGo.AddComponent<MainGateLock>();
        gl.escape = esc;
        gl.gateDoor = gate;

        // 2. its keypad (opened through the lock - the 8-digit code)
        GameObject kp = new GameObject("Gate Keypad");
        kp.transform.SetParent(lockGo.transform, false);
        KeypadController kc = kp.AddComponent<KeypadController>();
        SerializedObject kso = new SerializedObject(kc);
        kso.FindProperty("passcode").stringValue = GateCode;
        kso.ApplyModifiedPropertiesWithoutUndo();
        gl.keypad = kc;

        // 3. the door itself can no longer just be opened
        SerializedObject so = new SerializedObject(gate);
        so.FindProperty("isLocked").boolValue = true;
        so.FindProperty("requiredKey").stringValue = "MAIN GATE (key + code)";
        so.ApplyModifiedPropertiesWithoutUndo();

        // 4. the Main Gate Key in the last room: beside the acid bath, once it is drained
        bool keyMade = MakeKey(scene, template, "Main Gate Key", "Asylum Variant/2ndFloor/Bathroom", "Asylum Variant/2ndFloor/Bathroom/Bath (1)",
                        k => { k.solvedPuzzle = Comp<AcidDrainPuzzle>(scene, "Asylum Variant/2ndFloor/Bathroom/Bath (1)"); k.objectiveId = "Acid_Bathroom"; }, Vector3.zero, true);

        // 5. eight notes, one digit each
        string[,] notes =
        {
            { "Asylum Variant/1stFloor/DayRoom", "TableSchool", "They make us watch the films again and again. Someone scratched into the desk:\n\n[ GATE CODE - digit 1 of 8:  " + GateCode[0] + " ]" },
            { "Asylum Variant/1stFloor/Gatehouse", "TableOffice", "Night guard log: the Director changed the gate code again. I only remember the start.\n\n[ GATE CODE - digit 2 of 8:  " + GateCode[1] + " ]" },
            { "Asylum Variant/1stFloor/Stock", "Box_V1", "Written on the box in red crayon, the same number over and over.\n\n[ GATE CODE - digit 3 of 8:  " + GateCode[2] + " ]" },
            { "Asylum Variant/1stFloor/VisitRoom", "TableWhiteKitchen", "Visiting hours are over. Nobody visits any more. Mother, if you read this, the gate wants:\n\n[ GATE CODE - digit 4 of 8:  " + GateCode[3] + " ]" },
            { "Asylum Variant/2ndFloor/ExamRoom", "TableOffice", "Patient 41 screamed numbers during the X-ray. The doctor wrote one down.\n\n[ GATE CODE - digit 5 of 8:  " + GateCode[4] + " ]" },
            { "Asylum Variant/2ndFloor/Office", "TableOffice", "Memo from the Director: nobody leaves without the full code. Not even you.\n\n[ GATE CODE - digit 6 of 8:  " + GateCode[5] + " ]" },
            { "Asylum Variant/2ndFloor/Classroom", "TableOffice", "Today's lesson: counting. The children counted until they stopped breathing.\n\n[ GATE CODE - digit 7 of 8:  " + GateCode[6] + " ]" },
            { "Asylum Variant/Basement/HallUnderground", "TableOffice", "The last number. He laughs every time someone gets this far.\n\n[ GATE CODE - digit 8 of 8:  " + GateCode[7] + " ]" },
        };
        int notesMade = 0;
        for (int i = 0; i < notes.GetLength(0); i++)
            if (MakeNote(scene, noteTemplate, i + 1, notes[i, 0], notes[i, 1], notes[i, 2])) notesMade++;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = lockGo;
        Debug.Log("[TLC] Main gate now needs the Main Gate Key (" + (keyMade ? "Bathroom" : "NOT created") + ") + the 8-digit code " + GateCode +
                  " (" + notesMade + "/8 code notes placed). Scene saved.");
        return true;
    }

    private static bool MakeKey(Scene scene, GameObject template, string keyName, string roomPath, string anchorPath,
                                System.Action<GateKeyReveal> condition, Vector3 offset, bool besideAnchor = false)
    {
        Transform room = FindByPath(scene, roomPath)?.transform;
        GameObject anchor = FindByPath(scene, anchorPath);
        if (room == null || anchor == null) { Debug.LogWarning("[TLC] Main gate: '" + anchorPath + "' not found - " + keyName + " skipped."); return false; }

        Bounds ab = BoundsOf(anchor);
        Vector3 at = ab.center + offset;
        if (besideAnchor)
        {
            // next to the bath, on the side that faces the middle of the room
            Vector3 toRoom = BoundsOf(room.gameObject).center - ab.center; toRoom.y = 0f;
            if (toRoom.sqrMagnitude < 0.01f) toRoom = Vector3.forward;
            toRoom.Normalize();
            float reach = Mathf.Abs(toRoom.x) * ab.extents.x + Mathf.Abs(toRoom.z) * ab.extents.z;
            at = ab.center + toRoom * (reach + 0.35f);
        }

        GameObject holder = new GameObject("GateKey_" + keyName.Replace("Gate Key (", "").Replace(")", "").Replace(" ", ""));
        holder.transform.SetParent(room, false);
        holder.transform.position = at;
        GameObject drop = new GameObject("DropPoint");
        drop.transform.SetParent(holder.transform, false);

        GameObject key = Object.Instantiate(template, holder.transform, true);   // keep the key's world size / rotation
        key.name = keyName;
        key.transform.position = holder.transform.position;
        key.SetActive(false);
        SerializedObject so = new SerializedObject(key.GetComponent<KeyItem>());
        so.FindProperty("keyName").stringValue = keyName;
        so.FindProperty("promptMessage").stringValue = "Press E to pick up " + keyName;
        so.FindProperty("wakesUpEnemy").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        GateKeyReveal rev = holder.AddComponent<GateKeyReveal>();
        rev.key = key;
        rev.dropPoint = drop.transform;
        condition(rev);
        return true;
    }

    private static bool MakeNote(Scene scene, GameObject template, int n, string roomPath, string anchorName, string text)
    {
        Transform room = FindByPath(scene, roomPath)?.transform;
        if (room == null) { Debug.LogWarning("[TLC] Gate code note " + n + ": room '" + roomPath + "' not found."); return false; }
        Transform anchor = room.Find(anchorName);
        if (anchor == null)
            foreach (Transform c in room) if (c.name.StartsWith(anchorName)) { anchor = c; break; }
        Bounds ab = anchor != null ? BoundsOf(anchor.gameObject) : BoundsOf(room.gameObject);

        GameObject note = Object.Instantiate(template, room, true);   // keep the note's world size / rotation
        note.name = "GateCodeNote_" + n;
        note.transform.rotation = template.transform.rotation;
        note.transform.position = new Vector3(ab.center.x, (anchor != null ? ab.max.y : ab.min.y) + 0.01f, ab.center.z);
        note.SetActive(true);

        // make it findable: a bigger sheet resting on the surface, with a faint warm light over it
        note.transform.localScale = note.transform.localScale * NoteScale;
        Bounds nb = BoundsOf(note);
        float top = anchor != null ? ab.max.y : ab.min.y;
        note.transform.position += Vector3.up * (top + 0.005f - nb.min.y);
        GameObject glow = new GameObject("NoteGlow");
        glow.transform.SetParent(note.transform, true);
        glow.transform.position = note.transform.position + Vector3.up * 0.35f;
        Light l = glow.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.88f, 0.65f); l.range = 1.3f; l.intensity = 0.9f; l.shadows = LightShadows.None;

        SerializedObject so = new SerializedObject(note.GetComponent<ReadableNote>());
        so.FindProperty("noteText").stringValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (anchor == null) Debug.LogWarning("[TLC] Gate code note " + n + ": '" + anchorName + "' not found in " + roomPath + " - placed in the middle of the room, please move it.");
        return true;
    }

    // ------------------------------------------------------------------ helpers
    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private static Bounds BoundsOf(GameObject go)
    {
        bool any = false;
        Bounds b = new Bounds(go.transform.position, Vector3.one * 0.5f);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Bounds rb = r.bounds;
            if (rb.size.sqrMagnitude < 1e-6f) continue;          // inactive renderers report empty bounds
            if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
        }
        return b;
    }

    /// <summary>Finds an object by its Hierarchy path, also when it (or a parent) is inactive.</summary>
    private static GameObject FindByPath(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            Transform t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static T Comp<T>(Scene scene, string path) where T : Component
    {
        GameObject g = FindByPath(scene, path);
        return g != null ? g.GetComponent<T>() : null;
    }
}
#endif
