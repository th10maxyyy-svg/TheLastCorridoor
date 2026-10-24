using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Blood for the body-cut scene: a wet, glossy gash with dark lips, blood beads along the cut, rivulets that run across
/// the body and down its side, drips that fall to the floor, and pools (on the body and on the floor) that keep
/// spreading for a while. Lit, glossy materials so it catches the light like real wet blood.
/// </summary>
public class BloodFX : MonoBehaviour
{
    // ------------------------------------------------------------------ materials
    private static Material wet, dark, stain, drop;
    private static Texture2D dot;

    public static Material Wet => wet != null ? wet : (wet = Lit(new Color(0.23f, 0.005f, 0.005f), 0.88f));
    public static Material Dark => dark != null ? dark : (dark = Lit(new Color(0.045f, 0.0f, 0.0f), 0.7f));
    public static Material Stain => stain != null ? stain : (stain = Lit(new Color(0.3f, 0.02f, 0.02f), 0.35f));

    private static Material Lit(Color c, float smooth)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
        return m;
    }

    /// <summary>Soft round dot for blood drops (particles are round, not square).</summary>
    public static Material Drop
    {
        get
        {
            if (drop != null) return drop;
            if (dot == null)
            {
                dot = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float d = new Vector2(x - 15.5f, y - 15.5f).magnitude / 15.5f;
                        float a = Mathf.Clamp01((1f - d) * 3f);
                        float hi = 0.75f + 0.25f * Mathf.Clamp01(1f - new Vector2(x - 11f, y - 20f).magnitude / 6f);   // a little wet highlight
                        dot.SetPixel(x, y, new Color(hi, hi, hi, a));
                    }
                dot.Apply();
            }
            drop = new Material(Shader.Find("Sprites/Default")) { mainTexture = dot };
            return drop;
        }
    }

    // ------------------------------------------------------------------ building blocks

    public static LineRenderer Line(Transform parent, Material m, string name)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.rotation = Quaternion.LookRotation(Vector3.down);   // flat on the skin
        LineRenderer l = g.AddComponent<LineRenderer>();
        l.sharedMaterial = m;
        l.alignment = LineAlignment.TransformZ;
        l.generateLightingData = true;
        l.numCapVertices = 3;
        l.numCornerVertices = 2;
        l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        l.positionCount = 0;
        return l;
    }

    /// <summary>A flat, irregular puddle (grows with transform scale).</summary>
    public static GameObject Puddle(Transform parent, Vector3 pos, Vector3 along, Material m, int seed)
    {
        GameObject g = new GameObject("BloodPool");
        g.transform.SetParent(parent, true);
        g.transform.position = pos;
        Vector3 f = Vector3.ProjectOnPlane(along, Vector3.up);
        g.transform.rotation = Quaternion.LookRotation(f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward, Vector3.up);
        int n = 40;
        Vector3[] v = new Vector3[n + 1]; int[] tri = new int[n * 3]; Vector3[] nor = new Vector3[n + 1]; Vector2[] uv = new Vector2[n + 1];
        System.Random r = new System.Random(seed);
        float p1 = (float)r.NextDouble() * 6f, p2 = (float)r.NextDouble() * 6f;
        v[0] = Vector3.zero; nor[0] = Vector3.up; uv[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            float rad = 0.5f * (1f + 0.22f * Mathf.Sin(a * 3f + p1) + 0.12f * Mathf.Sin(a * 7f + p2) + 0.08f * (float)r.NextDouble());
            v[i + 1] = new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad);
            nor[i + 1] = Vector3.up; uv[i + 1] = new Vector2(v[i + 1].x + 0.5f, v[i + 1].z + 0.5f);
            tri[i * 3] = 0; tri[i * 3 + 1] = 1 + (i + 1) % n; tri[i * 3 + 2] = 1 + i;
        }
        Mesh mesh = new Mesh { vertices = v, triangles = tri, normals = nor, uv = uv };
        mesh.RecalculateBounds();
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.transform.localScale = new Vector3(0.001f, 1f, 0.001f);
        return g;
    }

    public static ParticleSystem Drops(Transform parent, string name, int max, float gravity = 1f, bool collide = true)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.01f, 0.01f), new Color(0.2f, 0f, 0f));
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.enabled = false;
        var col = ps.collision; col.enabled = collide; col.type = ParticleSystemCollisionType.World; col.dampen = 1f; col.bounce = 0f; col.lifetimeLoss = 0.6f;
        col.quality = ParticleSystemCollisionQuality.Medium;
        ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = Drop;
        pr.renderMode = ParticleSystemRenderMode.Stretch; pr.velocityScale = 0.025f; pr.lengthScale = 1.1f;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    public static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life)
    {
        var e = new ParticleSystem.EmitParams { position = pos, velocity = vel, startSize = size, startLifetime = life, applyShapeToPosition = false };
        ps.Emit(e, 1);
    }

    // ------------------------------------------------------------------ the flow after the cut (keeps going on its own)

    private class Rivulet
    {
        public LineRenderer line; public List<Vector3> pts = new List<Vector3>();
        public Vector3 pos, dir; public float speed, width, left; public bool down, done;
    }

    private Collider body;
    private Bounds bb;
    private ParticleSystem drips;
    private readonly List<Rivulet> rivulets = new List<Rivulet>();
    private readonly List<KeyValuePair<Transform, Vector2>> pools = new List<KeyValuePair<Transform, Vector2>>();   // pool, (target size, speed)
    private float life;
    private GameObject floorPool;
    private Vector3 floorPoolPos;
    private int dripsLanded;

    /// <summary>Starts blood running out of the cut a-b, across the body and over its edge.</summary>
    public void StartFlow(Collider bodyCol, Bounds bodyBounds, Vector3 a, Vector3 b, int count)
    {
        body = bodyCol; bb = bodyBounds;
        drips = Drops(transform, "Drips", 300);
        Vector3 cut = (b - a); cut.y = 0f;
        Vector3 side = Vector3.Cross(cut.normalized, Vector3.up);
        for (int i = 0; i < count; i++)
        {
            float k = Random.Range(0.15f, 0.85f);
            Rivulet r = new Rivulet
            {
                line = Line(transform, Wet, "Rivulet"),
                pos = Vector3.Lerp(a, b, k),
                dir = ((i % 2 == 0 ? side : -side) + cut.normalized * Random.Range(-0.25f, 0.25f)).normalized,
                speed = Random.Range(0.035f, 0.07f),
                width = Random.Range(0.006f, 0.011f),
                left = Random.Range(1.6f, 3.2f)
            };
            r.pts.Add(r.pos + Vector3.up * 0.004f);
            rivulets.Add(r);
        }
        life = 14f;
    }

    public void AddPool(Transform pool, float target, float speed) => pools.Add(new KeyValuePair<Transform, Vector2>(pool, new Vector2(target, speed)));

    void Update()
    {
        if (life <= 0f) return;
        life -= Time.deltaTime;
        float dt = Time.deltaTime;

        foreach (Rivulet r in rivulets)
        {
            if (r.done) continue;
            r.left -= dt;
            if (!r.down)
            {
                Vector3 next = r.pos + r.dir * r.speed * dt;
                if (!OnTop(next, out Vector3 surf))
                {
                    r.down = true;   // reached the edge: runs down the side
                }
                else
                {
                    r.pos = surf;
                    r.speed = Mathf.Max(0.012f, r.speed - dt * 0.006f);
                }
            }
            else
            {
                r.pos += Vector3.down * 0.12f * dt + r.dir * 0.002f * dt;
                if (r.pos.y < bb.min.y + 0.01f)
                {
                    r.done = true;
                    DripFrom(r.pos);
                }
            }
            if (r.left <= 0f && !r.down) r.done = true;   // ran out of blood before the edge
            if ((r.pos - r.pts[r.pts.Count - 1]).sqrMagnitude > 0.00004f) r.pts.Add(r.pos + Vector3.up * 0.004f + (r.down ? r.dir * 0.004f : Vector3.zero));
            r.line.positionCount = r.pts.Count;
            r.line.SetPositions(r.pts.ToArray());
            r.line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.85f, 0.75f), new Keyframe(1f, 0.35f));
            r.line.widthMultiplier = r.width;
            if (r.down) r.line.transform.rotation = Quaternion.LookRotation(-r.dir);   // the stream on the side faces outwards
        }

        // drops keep falling from the edges for a while
        foreach (Rivulet r in rivulets)
            if (r.done && r.down && Random.value < dt * 2.5f * Mathf.Clamp01(life / 8f)) DripFrom(r.pos);

        for (int i = 0; i < pools.Count; i++)
        {
            Transform p = pools[i].Key;
            if (p == null) continue;
            float s = Mathf.MoveTowards(p.localScale.x, pools[i].Value.x, pools[i].Value.y * dt);
            p.localScale = new Vector3(s, 1f, s * 0.8f);
        }
    }

    private bool OnTop(Vector3 p, out Vector3 surf)
    {
        surf = p;
        Vector3 from = new Vector3(p.x, bb.max.y + 0.3f, p.z);
        if (body != null && body.enabled && body.Raycast(new Ray(from, Vector3.down), out RaycastHit h, 1f)) { surf = h.point; return true; }
        // the body collider is switched off after the cut: stay inside the top of the box
        Vector3 local = p - bb.center;
        if (Mathf.Abs(local.x) < bb.extents.x - 0.01f && Mathf.Abs(local.z) < bb.extents.z - 0.01f) { surf = new Vector3(p.x, p.y, p.z); return true; }
        return false;
    }

    private void DripFrom(Vector3 p)
    {
        Emit(drips, p, new Vector3(Random.Range(-0.05f, 0.05f), -0.2f, Random.Range(-0.05f, 0.05f)), Random.Range(0.006f, 0.011f), 2f);
        if (floorPool == null && Physics.Raycast(p + Vector3.down * 0.05f, Vector3.down, out RaycastHit h, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            floorPoolPos = h.point + Vector3.up * 0.003f;
            floorPool = Puddle(transform, floorPoolPos, Random.insideUnitSphere, Wet, Random.Range(0, 9999));
            AddPool(floorPool.transform, 0.32f, 0.03f);
        }
    }
}
