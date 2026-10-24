using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A small glint that catches the eye: every few seconds a pick-up item lying in the world (key, fuse, crank...)
/// flashes a tiny star of light, so it can be spotted in the dark - but only when the player is near and it is on screen.
/// Not while the item is carried. One shared particle system for all items (cheap). Added automatically by KeyItem.
/// </summary>
public class ItemGlint : MonoBehaviour
{
    private static readonly List<ItemGlint> all = new List<ItemGlint>();
    private static ParticleSystem ps;
    private static float nextScan;
    private static Transform player;
    private Renderer[] rends;
    private float nextGlint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); ps = null; nextScan = 0f; player = null; }

    void OnEnable() { all.Add(this); rends = GetComponentsInChildren<Renderer>(); nextGlint = Time.time + Random.Range(0.5f, 3f); }
    void OnDisable() { all.Remove(this); }

    void Update()
    {
        if (all.Count == 0 || all[0] != this || Time.time < nextScan) return;   // the first item does the work for all
        nextScan = Time.time + 0.2f;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 eye = cam.transform.position;
        if (player == null) { Inventory inv = FindAnyObjectByType<Inventory>(); if (inv != null) player = inv.transform; }
        foreach (ItemGlint g in all)
        {
            if (g == null || Time.time < g.nextGlint) continue;
            g.nextGlint = Time.time + Random.Range(2.2f, 4.2f);
            if (player != null && g.transform.IsChildOf(player)) continue;   // carried
            if (!g.Bounds(out Bounds b)) continue;
            if ((b.center - eye).sqrMagnitude > 10f * 10f) continue;
            Vector3 vp = cam.WorldToViewportPoint(b.center);
            if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;
            if (Physics.Linecast(eye, b.center, out RaycastHit h, ~0, QueryTriggerInteraction.Ignore) && !h.transform.IsChildOf(g.transform)) continue;
            Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3(Random.Range(-0.6f, 0.6f), 0.7f, Random.Range(-0.6f, 0.6f)));
            Emit(p + (eye - p).normalized * 0.02f);
        }
    }

    private bool Bounds(out Bounds b)
    {
        b = new Bounds(); bool any = false;
        if (rends == null) return false;
        foreach (Renderer r in rends)
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }

    private static void Emit(Vector3 p)
    {
        if (ps == null) ps = Make();
        var e = new ParticleSystem.EmitParams { position = p, startSize = Random.Range(0.07f, 0.11f), startLifetime = 0.45f, rotation = Random.Range(0f, 90f), applyShapeToPosition = false };
        ps.Emit(e, 1);
    }

    private static ParticleSystem Make()
    {
        GameObject g = new GameObject("ItemGlints");
        ParticleSystem s = g.AddComponent<ParticleSystem>();
        s.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = s.main; main.loop = false; main.playOnAwake = false; main.startSpeed = 0f; main.maxParticles = 64;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(1f, 0.95f, 0.8f, 1f);
        var em = s.emission; em.rateOverTime = 0f;
        var sh = s.shape; sh.enabled = false;
        var sz = s.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f)));
        var rot = s.rotationOverLifetime; rot.enabled = true; rot.z = 1.5f;
        Texture2D t = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = Mathf.Abs(x - 31.5f) / 31.5f, dy = Mathf.Abs(y - 31.5f) / 31.5f;
                float star = Mathf.Max(Mathf.Clamp01(1f - dx * 9f) * (1f - dy), Mathf.Clamp01(1f - dy * 9f) * (1f - dx));   // 4-point star
                float core = Mathf.Clamp01(1f - new Vector2(dx, dy).magnitude * 3.2f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(star * star + core)));
            }
        t.Apply();
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = t };
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return s;
    }
}
