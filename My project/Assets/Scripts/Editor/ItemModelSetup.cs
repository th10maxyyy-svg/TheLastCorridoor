#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// REAL 3D MODELS FOR THE ITEMS. Many keys / items in the game level were still placeholder cubes.
/// This swaps every placeholder cube for a real model (the item object itself, its name, script, save id and position stay
/// exactly the same - only what you SEE changes):
///   all room keys     - old iron/brass key (each key gets its own tint: blue key, rusty key, red key ...)
///   Main Gate Key     - big ornate key
///   crank handle      - iron crank handle (Machine Room)          rusty crank - the same, rusty
///   Scalpel           - surgical scalpel                          headlamp    - head torch
/// The models are in Assets/Models/Items. Each model lies flat on its table, its click box is fitted to the model,
/// and in the hand it points forward. Runs once by itself and saves the scene;
/// run again from: The Last Corridor / Replace Item Cubes With Models.
/// </summary>
[InitializeOnLoad]
public static class ItemModelSetup
{
    private const string PrefKey = "TLC_ItemModels_v1_";
    private const string SceneName = "neww";
    private const string Folder = "Assets/Models/Items/";
    private static bool triedThisSession;

    static ItemModelSetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += TryAuto; return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "TLC_Key.obj") == null) return;   // models not imported yet
        triedThisSession = true;
        if (Run()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Replace Item Cubes With Models")]
    private static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[TLC] Stop Play mode first."); return; }
        Run();
    }

    // ------------------------------------------------------------------ what each item becomes
    private class Look { public string model; public float length; public Color tint; public bool procCrank, rusty; }

    private static Look LookFor(GameObject item)
    {
        if (item.GetComponent<HeadlampPickup>() != null)
            return new Look { model = "Headlamp/TLC_Headlamp.obj", length = 0.24f, tint = Color.white };
        KeyItem k = item.GetComponent<KeyItem>();
        if (k == null) return null;
        string n = (k.GetKeyName() ?? "").ToLowerInvariant();
        if (n.Contains("crank")) return new Look { procCrank = true, length = 0.34f, rusty = n.Contains("rusty") };
        if (n.Contains("scalpel")) return new Look { model = "Scalpel/TLC_Scalpel.obj", length = 0.16f, tint = Color.white };
        if (n.Contains("main gate")) return new Look { model = "GateKey/TLC_GateKey.obj", length = 0.2f, tint = new Color(1f, 0.85f, 0.5f) };
        if (n.Contains("key") || n == "redkey" || n == "escapekey")
        {
            Color c = new Color(0.78f, 0.72f, 0.6f);                         // old brass / iron
            if (n.Contains("blue")) c = new Color(0.45f, 0.6f, 1f);
            else if (n.Contains("red")) c = new Color(1f, 0.45f, 0.4f);
            else if (n.Contains("rusty")) c = new Color(0.75f, 0.42f, 0.25f);
            else if (n.Contains("morgue")) c = new Color(0.55f, 0.55f, 0.6f);
            else if (n.Contains("surgery") || n.Contains("pharmacy") || n.Contains("exam")) c = new Color(0.9f, 0.9f, 0.95f);
            else if (n.Contains("guard")) c = new Color(0.62f, 0.6f, 0.55f);
            return new Look { model = "TLC_Key.obj", length = 0.11f, tint = c };
        }
        return null;
    }

    // ------------------------------------------------------------------ run
    private static bool Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        string path = null;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.StartsWith("Assets/") && System.IO.Path.GetFileNameWithoutExtension(p) == SceneName) { path = p; break; }
        }
        if (path == null) { Debug.LogWarning("[TLC] Item models: scene '" + SceneName + "' not found."); return false; }

        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        List<string> done = new List<string>(), skipped = new List<string>();
        try
        {
            List<GameObject> items = new List<GameObject>();
            foreach (KeyItem k in Object.FindObjectsByType<KeyItem>(FindObjectsInactive.Include)) items.Add(k.gameObject);
            foreach (HeadlampPickup h in Object.FindObjectsByType<HeadlampPickup>(FindObjectsInactive.Include)) items.Add(h.gameObject);

            foreach (GameObject item in items)
            {
                if (item.scene != scene) continue;
                if (!IsPlaceholder(item)) continue;                      // already a real model (fuse, valve, ice, pendulum ...)
                Look look = LookFor(item);
                if (look == null) { skipped.Add(item.name); continue; }
                if (Apply(item, look)) done.Add(item.name); else skipped.Add(item.name + " (model missing)");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        bool missing = skipped.Exists(x => x.Contains("model missing"));
        Debug.Log("[TLC] Item models: " + done.Count + " placeholder cubes replaced (" + string.Join(", ", done) + ")" +
                  (skipped.Count > 0 ? "   kept as they were: " + string.Join(", ", skipped) : "") + ". Scene saved.");
        return !missing;   // a model file still missing -> tries again next time Unity starts
    }

    /// <summary>The item is still Unity's built-in cube (or already has our model child from an earlier run).</summary>
    private static bool IsPlaceholder(GameObject item)
    {
        if (item.transform.Find("Model") != null) return true;
        MeshFilter mf = item.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "Cube" && AssetDatabase.GetAssetPath(mf.sharedMesh).Contains("unity default resources")) return true;
        foreach (MeshFilter c in item.GetComponentsInChildren<MeshFilter>(true))
            if (c.sharedMesh != null && c.sharedMesh.name == "Cube" && AssetDatabase.GetAssetPath(c.sharedMesh).Contains("unity default resources")) return true;
        return false;
    }

    private static bool Apply(GameObject item, Look look)
    {
        // 1. build the visual first (if the model file is missing, leave the item untouched)
        GameObject visual = null;
        if (look.procCrank) visual = BuildCrank(look.rusty);
        else
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + look.model);
            if (asset == null) return false;
            visual = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Material m = MaterialFor(look);
            if (m != null) foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true)) { Material[] ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = m; r.sharedMaterials = ms; }
        }
        foreach (Collider c in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        // 2. where does the item rest? (the surface under the old cube)
        Bounds oldB = WorldBounds(item);
        float surfaceY = oldB.min.y;
        bool wasActive = item.activeSelf;

        // 3. remove the old cube look (and an older model from a previous run)
        Transform oldModel = item.transform.Find("Model");
        if (oldModel != null) Object.DestroyImmediate(oldModel.gameObject);
        MeshRenderer mr = item.GetComponent<MeshRenderer>(); if (mr != null) Object.DestroyImmediate(mr);
        MeshFilter mf = item.GetComponent<MeshFilter>(); if (mf != null) Object.DestroyImmediate(mf);
        foreach (MeshFilter c in item.GetComponentsInChildren<MeshFilter>(true))
            if (c != null && c.sharedMesh != null && c.sharedMesh.name == "Cube")
            {
                MeshRenderer cr = c.GetComponent<MeshRenderer>(); if (cr != null) Object.DestroyImmediate(cr);
                Object.DestroyImmediate(c);
            }

        // 4. the item root gets a clean, uniform scale (a cube squashed into a key shape would squash the model too)
        Transform root = item.transform;
        // other children (lights etc.) keep exactly their place and size
        List<Transform> kids = new List<Transform>(); List<Vector3> kp = new List<Vector3>(), ks = new List<Vector3>(); List<Quaternion> kr = new List<Quaternion>();
        foreach (Transform c in root) { kids.Add(c); kp.Add(c.position); kr.Add(c.rotation); ks.Add(c.lossyScale); }
        Vector3 pls = root.parent != null ? root.parent.lossyScale : Vector3.one;
        root.localScale = new Vector3(1f / Safe(pls.x), 1f / Safe(pls.y), 1f / Safe(pls.z));
        Vector3 e = root.eulerAngles; root.rotation = Quaternion.Euler(0f, e.y, 0f);   // lying flat, same direction as before
        for (int i = 0; i < kids.Count; i++)
        {
            if (kids[i] == null) continue;
            kids[i].SetPositionAndRotation(kp[i], kr[i]);
            Vector3 rl = root.lossyScale;
            kids[i].localScale = new Vector3(ks[i].x / Safe(rl.x), ks[i].y / Safe(rl.y), ks[i].z / Safe(rl.z));
        }

        // 5. orient the model: longest side forward (Z), thinnest side up (Y) -> lies flat, points forward in the hand
        visual.name = "Model";
        visual.transform.SetParent(root, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        Bounds lb = LocalBounds(visual.transform, root);
        Vector3 s = lb.size;
        int longest = s.x >= s.y && s.x >= s.z ? 0 : (s.y >= s.z ? 1 : 2);
        int thinnest = s.x <= s.y && s.x <= s.z ? 0 : (s.y <= s.z ? 1 : 2);
        if (thinnest == longest) thinnest = (longest + 1) % 3;
        Vector3 fwd = Axis(longest), up = Axis(thinnest);
        visual.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(fwd, up));
        lb = LocalBounds(visual.transform, root);
        float scale = look.length / Mathf.Max(0.0001f, Mathf.Max(lb.size.x, Mathf.Max(lb.size.y, lb.size.z)));
        visual.transform.localScale = Vector3.one * scale;
        lb = LocalBounds(visual.transform, root);
        // centred on the item, resting on the surface the cube stood on
        visual.transform.localPosition -= new Vector3(lb.center.x, 0f, lb.center.z);
        root.position = new Vector3(root.position.x, surfaceY + 0.002f, root.position.z);
        lb = LocalBounds(visual.transform, root);
        visual.transform.localPosition += new Vector3(0f, -lb.min.y, 0f);
        lb = LocalBounds(visual.transform, root);

        // 6. click box fitted to the model (a little bigger, so small items are easy to pick up)
        foreach (Collider c in item.GetComponents<Collider>()) if (!(c is BoxCollider)) Object.DestroyImmediate(c);
        BoxCollider box = item.GetComponent<BoxCollider>();
        if (box == null) box = item.AddComponent<BoxCollider>();
        box.isTrigger = false;
        box.center = lb.center;
        box.size = new Vector3(Mathf.Max(lb.size.x + 0.03f, 0.09f), Mathf.Max(lb.size.y + 0.03f, 0.06f), Mathf.Max(lb.size.z + 0.03f, 0.09f));

        foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
        item.SetActive(wasActive);
        EditorUtility.SetDirty(item);
        return true;
    }

    private static float Safe(float v) => Mathf.Abs(v) < 1e-5f ? 1f : v;
    private static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

    // works for inactive objects too (Renderer.bounds is empty while an object is inactive)
    private static Bounds WorldBounds(GameObject go)
    {
        bool any = false; Bounds b = new Bounds(go.transform.position, Vector3.zero);
        foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            Bounds mb = f.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 w = f.transform.TransformPoint(c);
                if (!any) { b = new Bounds(w, Vector3.zero); any = true; } else b.Encapsulate(w);
            }
        }
        return b;
    }

    /// <summary>Bounds of all meshes under 'model', expressed in 'space' (exact: uses the mesh vertices' box corners).</summary>
    private static Bounds LocalBounds(Transform model, Transform space)
    {
        bool any = false; Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        foreach (MeshFilter f in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            Bounds mb = f.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = space.InverseTransformPoint(f.transform.TransformPoint(c));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    // ------------------------------------------------------------------ materials
    private static Material MaterialFor(Look look)
    {
        string baseName = System.IO.Path.GetFileNameWithoutExtension(look.model);
        string dir = Folder + System.IO.Path.GetDirectoryName(look.model).Replace('\\', '/');
        if (!dir.EndsWith("/")) dir += "/";
        string matPath = dir + baseName + "_" + ColorUtility.ToHtmlStringRGB(look.tint) + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) return null;
        m = new Material(sh);
        Texture2D albedo = FindTex(dir, baseName, "_Albedo", "_BaseColor", "_Diffuse", "_Color");
        Texture2D normal = FindTex(dir, baseName, "_Normal");
        Texture2D metal = FindTex(dir, baseName, "_MetallicGloss", "_Metallic");
        if (albedo != null) m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", look.tint);
        if (normal != null)
        {
            string np = AssetDatabase.GetAssetPath(normal);
            TextureImporter ti = AssetImporter.GetAtPath(np) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP");
        }
        if (metal != null) { m.SetTexture("_MetallicGlossMap", metal); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 0.8f); }
        else { m.SetFloat("_Metallic", 0.7f); m.SetFloat("_Smoothness", 0.45f); }
        AssetDatabase.CreateAsset(m, matPath);
        return m;
    }

    private static Texture2D FindTex(string dir, string baseName, params string[] suffixes)
    {
        string stem = baseName.Replace("TLC_", "");
        foreach (string s in suffixes)
            foreach (string ext in new[] { ".png", ".jpg", ".jpeg" })
            {
                Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + stem + s + ext);
                if (t != null) return t;
                t = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + baseName + s + ext);
                if (t != null) return t;
            }
        return null;
    }

    // ------------------------------------------------------------------ crank handle (built from parts)
    private static GameObject BuildCrank(bool rusty)
    {
        Material metal = Mat(rusty ? "TLC_Crank_Rust" : "TLC_Crank_Iron", rusty ? new Color(0.42f, 0.22f, 0.12f) : new Color(0.22f, 0.22f, 0.24f), rusty ? 0.15f : 0.75f, rusty ? 0.2f : 0.45f);
        Material wood = Mat("TLC_Crank_Wood", new Color(0.33f, 0.2f, 0.11f), 0f, 0.3f);
        GameObject root = new GameObject("Crank");
        // long shaft along Z with a square socket at the front, a bent arm at the back and a wooden grip
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.024f, 0.13f, 0.024f), metal);
        Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0f, 0.135f), Vector3.zero, new Vector3(0.034f, 0.034f, 0.035f), metal);
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0.055f, 0f, -0.13f), new Vector3(0f, 0f, 90f), new Vector3(0.022f, 0.065f, 0.022f), metal);
        Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 0f, -0.13f), Vector3.zero, new Vector3(0.032f, 0.032f, 0.032f), metal);
        Part(root.transform, PrimitiveType.Sphere, new Vector3(0.11f, 0f, -0.13f), Vector3.zero, new Vector3(0.03f, 0.03f, 0.03f), metal);
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0.11f, 0f, -0.175f), new Vector3(90f, 0f, 0f), new Vector3(0.036f, 0.045f, 0.036f), wood);
        return root;
    }

    private static void Part(Transform parent, PrimitiveType t, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(t);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localEulerAngles = euler; g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
    }

    private static Material Mat(string name, Color c, float metallic, float smooth)
    {
        string dir = "Assets/Models/Items/Crank";
        if (!AssetDatabase.IsValidFolder("Assets/Models")) AssetDatabase.CreateFolder("Assets", "Models");
        if (!AssetDatabase.IsValidFolder("Assets/Models/Items")) AssetDatabase.CreateFolder("Assets/Models", "Items");
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Models/Items", "Crank");
        string p = dir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smooth);
        AssetDatabase.CreateAsset(m, p);
        return m;
    }
}
#endif
