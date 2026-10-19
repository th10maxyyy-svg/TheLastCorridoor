using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PERFORMANCE FIX: scene mein ~100 point lights shadows ke saath hain (aur lighting bake nahi hui), is liye URP har frame
/// sainkron shadow maps bana raha tha - yahi game ke lag ki asal wajah thi.
/// Ye script sirf camera ke sab se qareeb kuch lights ki shadows ON rakhta hai, baqi lights roshni dete rehte hain lekin bina shadow ke.
/// Kisi scene mein lagane ki zaroorat nahi - ye khud ban jata hai.
/// </summary>
public class LightShadowBudget : MonoBehaviour
{
    [Tooltip("Ek waqt mein zyada se zyada kitni lights shadow banayein (point light = 6 shadow maps)")]
    public int maxShadowedLights = 4;
    [Tooltip("Is se door wali lights ki shadow kabhi nahi banegi (meters)")]
    public float maxShadowDistance = 25f;
    [Tooltip("Kitni dafa per second check kare")]
    public float updateInterval = 0.25f;

    private class TrackedLight
    {
        public Light light;
        public LightShadows originalShadows;
    }

    private readonly List<TrackedLight> lights = new List<TrackedLight>();
    private float nextUpdate;
    private float nextRescan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnStartup()
    {
        if (FindAnyObjectByType<LightShadowBudget>() != null) return;
        GameObject go = new GameObject("[LightShadowBudget]");
        DontDestroyOnLoad(go);
        go.AddComponent<LightShadowBudget>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Rescan();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Rescan();
    }

    private void Rescan()
    {
        // Keep what we already know so the ORIGINAL shadow setting isn't lost
        Dictionary<Light, LightShadows> known = new Dictionary<Light, LightShadows>();
        foreach (TrackedLight t in lights)
        {
            if (t.light != null) known[t.light] = t.originalShadows;
        }
        lights.Clear();

        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (l.type != LightType.Point && l.type != LightType.Spot) continue;

            LightShadows original = known.TryGetValue(l, out LightShadows s) ? s : l.shadows;
            if (original == LightShadows.None) continue;

            // Truly baked lights have no realtime shadow cost - leave them alone
            if (l.bakingOutput.isBaked && l.bakingOutput.lightmapBakeType == LightmapBakeType.Baked) continue;

            lights.Add(new TrackedLight { light = l, originalShadows = original });
        }
        nextRescan = Time.unscaledTime + 5f;
    }

    void LateUpdate()
    {
        if (Time.unscaledTime >= nextRescan) Rescan(); // picks up lights spawned later (prefabs, corridors...)
        if (Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + updateInterval;

        Camera cam = GetActiveCamera();
        if (cam == null) return;
        Vector3 viewPos = cam.transform.position;

        lights.RemoveAll(t => t.light == null);

        // Nearest active lights first
        lights.Sort((a, b) =>
            (a.light.transform.position - viewPos).sqrMagnitude.CompareTo((b.light.transform.position - viewPos).sqrMagnitude));

        int shadowed = 0;
        float maxDistSqr = maxShadowDistance * maxShadowDistance;
        foreach (TrackedLight t in lights)
        {
            bool visible = t.light.isActiveAndEnabled; // (not intensity: flickering lights would toggle shadows on/off constantly)
            bool close = (t.light.transform.position - viewPos).sqrMagnitude <= maxDistSqr;
            bool giveShadow = visible && close && shadowed < maxShadowedLights;

            LightShadows wanted = giveShadow ? t.originalShadows : LightShadows.None;
            if (t.light.shadows != wanted) t.light.shadows = wanted;
            if (giveShadow) shadowed++;
        }
    }

    private static Camera GetActiveCamera()
    {
        Camera best = null;
        foreach (Camera c in Camera.allCameras) // only enabled cameras (also the ending cinematic camera)
        {
            if (c.targetTexture != null) continue;
            if (best == null || c.depth > best.depth) best = c;
        }
        return best != null ? best : Camera.main;
    }
}
