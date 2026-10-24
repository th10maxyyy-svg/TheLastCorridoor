#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GAME EXTRAS for the game level (neww), runs once by itself and saves the scene:
///   - 6 MEDKITS on tables (Morgue, Guard Room, Surgery, Kitchen downstairs, Kitchen upstairs, Dining Room)
///   - the grandfather clock now gives a MEDKIT (it used to give a second, useless stock room key)
///   - a clearer HINT NOTE for the Morgue barrier code (2024) next to the electric box
/// Run again from: The Last Corridor / Setup Game Extras (old extras are removed first, nothing is doubled).
/// </summary>
[InitializeOnLoad]
public static class GameExtrasSetup
{
    private const string PrefKey = "TLC_GameExtras_v1_";
    private const string SceneName = "neww";
    private static bool triedThisSession;

    // room path, surface object name (starts with)
    private static readonly string[,] MedkitSpots =
    {
        { "Asylum Variant/Basement/Morgue",       "TableWhite" },
        { "Asylum Variant/1stFloor/GuardRoom",    "TableOffice" },
        { "Asylum Variant/2ndFloor/Surgery",      "TableWhite" },
        { "Asylum Variant/1stFloor/KitchenA",     "TableKitchen (1)" },
        { "Asylum Variant/2ndFloor/Kitchen",      "TableWhite" },
        { "Asylum Variant/1stFloor/DiningRoom",   "TableWhiteKitchen (3)" },
    };

    static GameExtrasSetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += TryAuto; return; }
        triedThisSession = true;
        if (Run()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Setup Game Extras (medkits, hints)")]
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
        if (path == null) { Debug.LogWarning("[TLC] Extras: scene '" + SceneName + "' not found."); return false; }

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        List<string> done = new List<string>(), warn = new List<string>();
        try
        {
            // clean up an older run
            foreach (Medkit m in Object.FindObjectsByType<Medkit>(FindObjectsInactive.Include)) Object.DestroyImmediate(m.gameObject);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t != null && t.name == "MorgueBarrierHint") Object.DestroyImmediate(t.gameObject);

            Material body = Mat("TLC_Medkit_Body", new Color(0.86f, 0.84f, 0.8f));
            Material red = Mat("TLC_Medkit_Cross", new Color(0.75f, 0.05f, 0.05f));

            // 1. medkits on tables
            for (int i = 0; i < MedkitSpots.GetLength(0); i++)
            {
                Transform room = FindByPath(scene, MedkitSpots[i, 0])?.transform;
                Transform surf = null;
                if (room != null) foreach (Transform c in room) if (c.name.StartsWith(MedkitSpots[i, 1])) { surf = c; break; }
                if (room == null || surf == null) { warn.Add(MedkitSpots[i, 0] + "/" + MedkitSpots[i, 1]); continue; }
                Bounds b = BoundsOf(surf.gameObject);
                // a corner of the table top (so it does not sit on a note in the middle)
                Vector3 pos = new Vector3(b.center.x + b.extents.x * 0.45f, b.max.y + 0.001f, b.center.z - b.extents.z * 0.35f);
                GameObject kit = MakeMedkit(body, red, room, pos, Quaternion.Euler(0f, 25f * i, 0f));
                kit.name = "Medkit_" + room.name;
                done.Add("medkit " + room.name);
            }

            // 2. the grandfather clock gives a medkit instead of a second stock room key
            GrandfatherClockPuzzle clock = Object.FindAnyObjectByType<GrandfatherClockPuzzle>(FindObjectsInactive.Include);
            if (clock != null)
            {
                GameObject reward = MakeMedkit(body, red, clock.transform.parent, clock.transform.position + clock.transform.forward * 0.5f, Quaternion.identity);
                reward.name = "Medkit_ClockReward";
                Medkit mk = reward.GetComponent<Medkit>(); mk.healAmount = 60f;
                reward.SetActive(false);
                SerializedObject so = new SerializedObject(clock);
                GameObject oldReward = so.FindProperty("stockRoomKey").objectReferenceValue as GameObject;
                so.FindProperty("stockRoomKey").objectReferenceValue = reward;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (oldReward != null && oldReward.GetComponent<KeyItem>() != null && oldReward.name.ToLower().Contains("stock")) oldReward.SetActive(false);
                done.Add("clock reward = big medkit");
            }
            else warn.Add("grandfather clock");

            // 3. a clear hint for the Morgue barrier code, on the floor in front of the electric box
            GameObject box = FindByPath(scene, "Asylum Variant/Basement/Morgue/electric_box_41");
            GameObject noteTemplate = FindByPath(scene, "Asylum Variant/1stFloor/GuardRoom/clue");
            if (box != null && noteTemplate != null && noteTemplate.GetComponent<ReadableNote>() != null)
            {
                Bounds bb = BoundsOf(box);
                Vector3 front = box.transform.forward; front.y = 0f; if (front.sqrMagnitude < 0.01f) front = Vector3.forward;
                Vector3 start = bb.center + front.normalized * (Mathf.Max(bb.extents.x, bb.extents.z) + 0.6f);
                Vector3 ground = start + Vector3.down * 0.5f;
                if (Physics.Raycast(start + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point;
                GameObject note = Object.Instantiate(noteTemplate, box.transform.parent, true);
                note.name = "MorgueBarrierHint";
                note.SetActive(true);
                note.transform.localScale = note.transform.localScale * 2.5f;
                note.transform.position = ground + Vector3.up * 0.02f;
                Bounds nb = BoundsOf(note);
                note.transform.position += Vector3.up * (ground.y + 0.01f - nb.min.y);
                SerializedObject so = new SerializedObject(note.GetComponent<ReadableNote>());
                so.FindProperty("noteText").stringValue =
                    "Electrician's scrap of paper:\n\"Barrier reset code = HOW MANY DAYS the patient in Seclusion A has been locked up, " +
                    "then the HOUR the night guard's shift ends.\nAsk them. They both wrote it down.\"";
                so.ApplyModifiedPropertiesWithoutUndo();
                GameObject glow = new GameObject("NoteGlow");
                glow.transform.SetParent(note.transform, true);
                glow.transform.position = note.transform.position + Vector3.up * 0.4f;
                Light l = glow.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.88f, 0.65f); l.range = 1.3f; l.intensity = 0.9f; l.shadows = LightShadows.None;
                done.Add("morgue code hint note");
            }
            else warn.Add("morgue electric box / note template");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        Debug.Log("[TLC] Game extras placed (" + done.Count + "): " + string.Join(", ", done) +
                  (warn.Count > 0 ? "   NOT FOUND: " + string.Join(", ", warn) : "") + ". Scene saved.");
        return warn.Count == 0;
    }

    // a small first-aid case: white box, red cross on top, handle, a faint glow so it can be found
    private static GameObject MakeMedkit(Material body, Material red, Transform parent, Vector3 pos, Quaternion rot)
    {
        GameObject kit = new GameObject("Medkit");
        kit.transform.SetParent(parent, true);
        kit.transform.SetPositionAndRotation(pos, rot);
        Part(kit.transform, "Case", PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), new Vector3(0.34f, 0.12f, 0.24f), body);
        Part(kit.transform, "Lid", PrimitiveType.Cube, new Vector3(0f, 0.125f, 0f), new Vector3(0.345f, 0.012f, 0.245f), body);
        Part(kit.transform, "CrossA", PrimitiveType.Cube, new Vector3(0f, 0.133f, 0f), new Vector3(0.14f, 0.006f, 0.045f), red);
        Part(kit.transform, "CrossB", PrimitiveType.Cube, new Vector3(0f, 0.133f, 0f), new Vector3(0.045f, 0.006f, 0.14f), red);
        Part(kit.transform, "Handle", PrimitiveType.Cube, new Vector3(0f, 0.075f, 0.128f), new Vector3(0.12f, 0.02f, 0.02f), red);
        BoxCollider col = kit.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.07f, 0f); col.size = new Vector3(0.38f, 0.16f, 0.28f);
        kit.AddComponent<Medkit>();
        GameObject glow = new GameObject("Glow");
        glow.transform.SetParent(kit.transform, false);
        glow.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        Light l = glow.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.5f); l.range = 1.2f; l.intensity = 0.7f; l.shadows = LightShadows.None;
        return kit;
    }

    private static void Part(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 size, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
    }

    private static Material Mat(string name, Color c)
    {
        string folder = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Materials");
        string path = folder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static Bounds BoundsOf(GameObject go)
    {
        bool any = false;
        Bounds b = new Bounds(go.transform.position, Vector3.one * 0.5f);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Bounds rb = r.bounds;
            if (rb.size.sqrMagnitude < 1e-6f) continue;
            if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
        }
        return b;
    }

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
}
#endif
