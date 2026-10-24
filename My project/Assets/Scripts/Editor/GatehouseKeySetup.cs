#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GATEHOUSE KEY:
///   - the Gatehouse (ground floor, where the VALVE for the acid bath is) is LOCKED and needs the "gatehouse key"
///   - the gatehouse key lies in the BASEMENT, Seclusion Room A, on the floor next to the skeleton
///   - a note in the ground-floor Kitchen (next to the oven clue) tells the player where it is
/// Runs once by itself and saves the scene; run again from: The Last Corridor / Setup Gatehouse Key.
/// </summary>
[InitializeOnLoad]
public static class GatehouseKeySetup
{
    private const string PrefKey = "TLC_GatehouseKey_v2_";
    private const string SceneName = "neww";
    private const string KeyName = "gatehouse key";
    private const string NoteText = "The night guard lost the gatehouse key in the basement - Seclusion Room A, right next to the skull. Nobody wants to go down there to get it.";
    private static bool triedThisSession;

    static GatehouseKeySetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += TryAuto; return; }
        triedThisSession = true;
        if (Run()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Setup Gatehouse Key")]
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
            // 1. the Gatehouse door: locked, needs the gatehouse key
            GameObject doorGo = FindPath(scene, "Asylum Variant/1stFloor/Gatehouse/Door_V1/Door");
            DoorController door = doorGo != null ? doorGo.GetComponent<DoorController>() : null;
            if (door == null) { Debug.LogWarning("[TLC] Gatehouse door not found."); return false; }
            SerializedObject so = new SerializedObject(door);
            so.FindProperty("isLocked").boolValue = true;
            so.FindProperty("requiredKey").stringValue = KeyName;
            so.ApplyModifiedProperties();

            // 2. the key (a copy of an ordinary room key), next to the skeleton in Seclusion Room A
            GameObject room = FindPath(scene, "Asylum Variant/Basement/SeclusionRoomA");
            if (room == null) { Debug.LogWarning("[TLC] Seclusion Room A not found."); return false; }
            Transform skeleton = null;
            foreach (Transform t in room.transform)
                if (t.name.ToLowerInvariant().Contains("skelet") || t.name.ToLowerInvariant().Contains("skull")) { skeleton = t; break; }
            if (skeleton == null) { Debug.LogWarning("[TLC] Skeleton in Seclusion Room A not found."); return false; }

            KeyItem key = null;
            foreach (KeyItem k in Object.FindObjectsByType<KeyItem>(FindObjectsInactive.Include))
                if (k.gameObject.scene == scene && (k.GetKeyName() ?? "").Trim().ToLowerInvariant() == KeyName) { key = k; break; }
            if (key == null)
            {
                KeyItem template = null;
                foreach (KeyItem k in Object.FindObjectsByType<KeyItem>(FindObjectsInactive.Include))
                {
                    string n = (k.GetKeyName() ?? "").Trim().ToLowerInvariant();
                    if (k.gameObject.scene == scene && (n == "visit room key" || n == "surgery key" || n == "exam room key")) { template = k; break; }
                }
                if (template == null) { Debug.LogWarning("[TLC] No room key to copy for the gatehouse key."); return false; }
                GameObject copy = Object.Instantiate(template.gameObject, room.transform);
                copy.name = KeyName;
                copy.SetActive(true);
                key = copy.GetComponent<KeyItem>();
                SerializedObject ks = new SerializedObject(key);
                ks.FindProperty("keyName").stringValue = KeyName;
                ks.FindProperty("promptMessage").stringValue = "Press E to pick up Gatehouse Key";
                SerializedProperty ev = ks.FindProperty("onPickUpEvent.m_PersistentCalls.m_Calls");
                if (ev != null) ev.ClearArray();
                ks.ApplyModifiedProperties();
                Undo.RegisterCreatedObjectUndo(copy, "Gatehouse key");
            }
            key.transform.SetParent(room.transform, true);
            Bounds sb = BoundsOf(skeleton);
            Transform roomDoor = null;
            foreach (DoorController d in room.GetComponentsInChildren<DoorController>(true)) { roomDoor = d.transform; break; }
            Vector3 dir = roomDoor != null ? roomDoor.position - sb.center : -skeleton.forward;
            dir.y = 0f; dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 spot = new Vector3(sb.center.x, sb.max.y, sb.center.z) + dir * (Mathf.Max(sb.extents.x, sb.extents.z) + 0.25f) + side * 0.2f;
            // a normal-sized key (11 cm), lying flat on the floor (the copy came out huge and standing up)
            NormalizeKey(key.transform, 0.11f);
            Physics.SyncTransforms();
            float floorY = sb.min.y;
            if (Physics.Raycast(spot + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(key.transform)) floorY = hit.point.y;
            key.transform.position = new Vector3(spot.x, floorY + 0.2f, spot.z);
            Bounds kb = BoundsOf(key.transform);
            key.transform.position += Vector3.up * (floorY + 0.004f - kb.min.y);
            EditorUtility.SetDirty(key.gameObject);

            // 3. the note in the ground-floor Kitchen, next to the oven clue
            GameObject clue = FindPath(scene, "Asylum Variant/1stFloor/KitchenA/clue");
            if (clue != null && FindPath(scene, "Asylum Variant/1stFloor/KitchenA/GatehouseKeyNote") == null)
            {
                GameObject note = Object.Instantiate(clue, clue.transform.parent);
                note.name = "GatehouseKeyNote";
                note.transform.position = clue.transform.position + clue.transform.right * 0.3f + Vector3.up * 0.002f;
                note.transform.rotation = clue.transform.rotation * Quaternion.Euler(0f, 0f, 14f);
                ReadableNote rn = note.GetComponent<ReadableNote>();
                if (rn != null) rn.noteText = NoteText;
                EditorUtility.SetDirty(note);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TLC] Gatehouse locked (gatehouse key); key placed in Seclusion Room A next to the skeleton (" + key.transform.position + "); note in the Kitchen. Scene saved.");
            ok = true;
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        return ok;
    }

    /// <summary>Scales the key so its longest side is 'length' metres and turns it so its flat side lies on the floor.</summary>
    private static void NormalizeKey(Transform k, float length)
    {
        Bounds lb = new Bounds(); bool any = false;
        foreach (MeshFilter mf in k.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = k.InverseTransformPoint(mf.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                if (!any) { lb = new Bounds(p, Vector3.zero); any = true; } else lb.Encapsulate(p);
            }
        }
        if (!any) return;
        Vector3 ls = k.lossyScale;
        Vector3 ws = new Vector3(Mathf.Abs(lb.size.x * ls.x), Mathf.Abs(lb.size.y * ls.y), Mathf.Abs(lb.size.z * ls.z));
        float longest = Mathf.Max(ws.x, Mathf.Max(ws.y, ws.z));
        if (longest > 1e-5f) k.localScale *= length / longest;
        int thin = ws.x <= ws.y && ws.x <= ws.z ? 0 : ws.y <= ws.z ? 1 : 2;
        Vector3 axis = Vector3.zero; axis[thin] = 1f;
        Vector3 thinWorld = k.TransformDirection(axis).normalized;
        k.rotation = Quaternion.FromToRotation(thinWorld, Vector3.up) * k.rotation;
        k.rotation = Quaternion.AngleAxis(65f, Vector3.up) * k.rotation;
    }

    private static Bounds BoundsOf(Transform t)
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
