using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// The acid bath. Before: a glowing, bubbling, fuming green acid (lights the room green, simmers).
/// With the Valve: the pipes clunk and squeal, the plug opens, a whirlpool forms, the acid gurgles away and sinks
/// slowly down the tub - revealing what is lying in it - until only a thin, smoking film is left on the bottom.
/// </summary>
public class AcidDrainPuzzle : MonoBehaviour, IInteractable
{
    [Header("Puzzle Settings")]
    public string requiredValveItem = "Valve";

    [Header("Visuals to Move")]
    [Tooltip("Pani/Tezaab ka 3D model (mesh) jisay neechay karna hai")]
    public Transform acidMesh;
    [Tooltip("Valve ka model jo puzzle complete hone par yahan pipe par show hoga")]
    public Transform valveMesh;

    [Header("Animation Settings")]
    public float drainDuration = 6f;
    [Tooltip("Tezaab kitna neechay jayega (Misaal ke tor par -1.5 likhne se wo 1.5 meter neechay chala jayega)")]
    public float drainAmountY = -1.5f;

    [Header("Audio")]
    public AudioClip valveTurnSound;
    public AudioClip acidDrainSound;
    private AudioSource audioSource;

    [Header("Events")]
    [Tooltip("Jab tezaab drain ho jaye to kya ho? (Yahan Key ka collider ON kar sakte hain)")]
    public UnityEvent onDrainComplete;

    private bool isSolved = false;
    /// <summary>Read by the objective / achievement system (GameProgress).</summary>
    public bool IsSolved => isSolved;
    private string SaveFlag => "AcidDrained_" + SaveManager.GetSceneObjectId(transform);

    // ---- look & feel
    private readonly List<Transform> surfaces = new List<Transform>();
    private readonly List<Vector3> surfaceStart = new List<Vector3>();
    private readonly List<Vector3> surfaceScale = new List<Vector3>();
    private readonly List<Material> acidMats = new List<Material>();
    private Light glow;
    private ParticleSystem fumes;
    private AudioSource simmer;
    private float bottomY;
    private bool fxReady;
    private static readonly Color AcidGlow = new Color(0.35f, 1f, 0.12f);

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f; audioSource.minDistance = 1.5f; audioSource.maxDistance = 9f;
        audioSource.rolloffMode = AudioRolloffMode.Linear; audioSource.dopplerLevel = 0f;
        if (valveTurnSound == null) valveTurnSound = Resources.Load<AudioClip>("HorrorAudio/TLC_Valve_Turn");
        if (acidDrainSound == null) acidDrainSound = Resources.Load<AudioClip>("HorrorAudio/TLC_Acid_Drain");

        SetupLook();

        if (SaveManager.instance != null && SaveManager.instance.HasFlag(SaveFlag))
        {
            isSolved = true;
            if (valveMesh != null) valveMesh.gameObject.SetActive(true);
            SetDrained(1f);
            ResidueLook();
            onDrainComplete?.Invoke();
            return;
        }

        // Shuru mein pipe par valve nahi hona chahiye
        if (valveMesh != null) valveMesh.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ the acid before it drains

    private void SetupLook()
    {
        if (acidMesh == null) return;
        // the scene has two copies of the acid surface lying on top of each other: both of them drain
        surfaces.Add(acidMesh);
        if (acidMesh.parent != null)
            foreach (Transform s in acidMesh.parent)
                if (s != acidMesh && s.name == acidMesh.name && Vector3.Distance(s.position, acidMesh.position) < 0.6f) surfaces.Add(s);
        foreach (Transform s in surfaces) { surfaceStart.Add(s.position); surfaceScale.Add(s.localScale); }

        // tub bottom (inside): just above the lowest point of the bath model
        Renderer bath = GetComponent<Renderer>();
        float top = acidMesh.position.y;
        bottomY = top + drainAmountY;
        if (bath != null) bottomY = Mathf.Max(bottomY, bath.bounds.min.y + 0.1f);
        bottomY = Mathf.Min(bottomY, top - 0.05f);

        // glowing, glossy acid
        foreach (Transform s in surfaces)
        {
            Renderer r = s.GetComponent<Renderer>();
            if (r == null) continue;
            Material m = r.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.22f, 0.75f, 0.08f, m.GetColor("_BaseColor").a));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.92f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", AcidGlow * 0.55f);
            }
            acidMats.Add(m);
        }

        // green light from the acid
        GameObject lg = new GameObject("AcidGlow");
        lg.transform.SetParent(transform, false);
        lg.transform.position = acidMesh.position + Vector3.up * 0.45f;
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point; glow.color = AcidGlow; glow.range = 3.2f; glow.intensity = 1.3f;
        glow.shadows = LightShadows.None;

        // fumes rising off it
        fumes = MakeFumes(acidMesh.position, SurfaceSize());

        // a low simmer
        AudioClip sim = Resources.Load<AudioClip>("HorrorAudio/TLC_Acid_Simmer");
        if (sim != null)
        {
            simmer = gameObject.AddComponent<AudioSource>();
            simmer.clip = sim; simmer.loop = true; simmer.spatialBlend = 1f; simmer.minDistance = 1f; simmer.maxDistance = 7f;
            simmer.rolloffMode = AudioRolloffMode.Linear; simmer.dopplerLevel = 0f;
            simmer.volume = 0.55f; simmer.Play();
        }
        BuildTubSurface();
        fxReady = true;
    }

    // ------------------------------------------------------------------ acid that fills the REAL shape of the tub
    // The old acid was a flat rectangle: it poked through the tub's sides and, going down, stuck out of the tub.
    // Now the surface is cut to the inside of the tub at every height (measured on the bath's own collider), so the
    // whole tub drains evenly, hugging the walls all the way to the bottom.
    private Collider bathCol;
    private Vector3 tubCenter;
    private const int Ring = 48, Levels = 14;
    private Vector3[][] outlines;      // inside outline of the tub, from the top level (0) to the bottom (Levels-1)
    private float topY;
    private Mesh tubMesh;
    private GameObject tubSurface;
    private Material tubMat;
    private LineRenderer tideMark;
    private Vector2 scroll;

    private void BuildTubSurface()
    {
        bathCol = GetComponent<MeshCollider>();
        if (bathCol == null) bathCol = GetComponent<Collider>();
        Renderer bath = GetComponent<Renderer>();
        if (bathCol == null || bath == null || acidMesh == null) return;
        topY = acidMesh.position.y;
        tubCenter = new Vector3(bath.bounds.center.x, topY, bath.bounds.center.z);
        // the real inside floor of the tub
        if (bathCol.Raycast(new Ray(tubCenter + Vector3.up * 0.05f, Vector3.down), out RaycastHit fl, 2f))
            bottomY = Mathf.Min(fl.point.y + 0.012f, topY - 0.05f);

        float maxR = Mathf.Max(bath.bounds.extents.x, bath.bounds.extents.z) * 1.6f;
        outlines = new Vector3[Levels][];
        int good = 0;
        for (int l = 0; l < Levels; l++)
        {
            float y = Mathf.Lerp(topY, bottomY, l / (Levels - 1f));
            outlines[l] = new Vector3[Ring];
            for (int i = 0; i < Ring; i++)
            {
                float a = i * Mathf.PI * 2f / Ring;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 o = new Vector3(tubCenter.x, y, tubCenter.z);
                if (bathCol.Raycast(new Ray(o, dir), out RaycastHit h, maxR)) { outlines[l][i] = h.point - dir * 0.006f; good++; }
                else outlines[l][i] = Vector3.zero;
            }
        }
        if (good < Ring * Levels / 2) { outlines = null; return; }   // could not measure the tub: keep the old flat acid
        // holes in the measurement: use the neighbour level / point
        for (int l = 0; l < Levels; l++)
            for (int i = 0; i < Ring; i++)
                if (outlines[l][i] == Vector3.zero)
                {
                    Vector3 f = Vector3.zero;
                    for (int k = 1; k < Ring && f == Vector3.zero; k++) f = outlines[l][(i + k) % Ring];
                    if (f == Vector3.zero && l > 0) f = outlines[l - 1][i];
                    outlines[l][i] = f == Vector3.zero ? tubCenter : new Vector3(f.x, Mathf.Lerp(topY, bottomY, l / (Levels - 1f)), f.z);
                }

        Renderer src = acidMesh.GetComponent<Renderer>();
        tubMat = src != null ? new Material(acidMats.Count > 0 ? acidMats[0] : src.sharedMaterial) : null;
        tubSurface = new GameObject("AcidSurface (tub shape)");
        tubSurface.transform.SetParent(transform, true);
        tubMesh = new Mesh(); tubMesh.MarkDynamic();
        tubSurface.AddComponent<MeshFilter>().sharedMesh = tubMesh;
        MeshRenderer mr = tubSurface.AddComponent<MeshRenderer>();
        mr.sharedMaterial = tubMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (tubMat != null) acidMats.Add(tubMat);
        foreach (Transform s in surfaces) { Renderer r = s.GetComponent<Renderer>(); if (r != null) r.enabled = false; }
        UpdateTubSurface(0f, 0f);
    }

    /// <summary>k = 0 full ... 1 drained. wobble moves the middle of the surface (the whirlpool pulls it down).</summary>
    private void UpdateTubSurface(float k, float dip)
    {
        if (outlines == null || tubMesh == null) return;
        float f = Mathf.Clamp01(k) * (Levels - 1);
        int l0 = Mathf.Min(Levels - 2, Mathf.FloorToInt(f));
        float t = f - l0;
        float y = Mathf.Lerp(topY, bottomY, Mathf.Clamp01(k)) + Mathf.Sin(Time.time * 1.3f) * 0.002f * (1f - k);
        Vector3[] v = new Vector3[Ring + 1];
        Vector2[] uv = new Vector2[Ring + 1];
        Vector3[] n = new Vector3[Ring + 1];
        int[] tri = new int[Ring * 3];
        v[0] = new Vector3(tubCenter.x, y - dip, tubCenter.z);
        for (int i = 0; i < Ring; i++)
        {
            Vector3 p = Vector3.Lerp(outlines[l0][i], outlines[l0 + 1][i], t);
            v[i + 1] = new Vector3(p.x, y, p.z);
        }
        for (int i = 0; i <= Ring; i++)
        {
            uv[i] = new Vector2(v[i].x * 1.2f, v[i].z * 1.2f);
            n[i] = Vector3.up;
            if (i < Ring) { tri[i * 3] = 0; tri[i * 3 + 1] = 1 + (i + 1) % Ring; tri[i * 3 + 2] = 1 + i; }
        }
        tubMesh.Clear();
        tubMesh.vertices = v; tubMesh.uv = uv; tubMesh.normals = n; tubMesh.triangles = tri;
        tubMesh.RecalculateBounds();
        if (tubMat != null)
        {
            scroll += new Vector2(0.03f, 0.02f) * Time.deltaTime * (1f + k * 4f);
            if (tubMat.HasProperty("_BaseMap")) tubMat.SetTextureOffset("_BaseMap", scroll);
            if (tubMat.HasProperty("_BumpMap")) tubMat.SetTextureOffset("_BumpMap", scroll);
        }
    }

    /// <summary>The dark, scorched line the acid leaves on the tub walls where it stood.</summary>
    private void MakeTideMark()
    {
        if (outlines == null || tideMark != null) return;
        GameObject g = new GameObject("AcidTideMark");
        g.transform.SetParent(transform, true);
        tideMark = g.AddComponent<LineRenderer>();
        tideMark.loop = true;
        tideMark.positionCount = Ring;
        for (int i = 0; i < Ring; i++) tideMark.SetPosition(i, outlines[0][i] + Vector3.up * 0.002f);
        tideMark.widthMultiplier = 0.018f;
        tideMark.alignment = LineAlignment.View;
        Material m = new Material(Shader.Find("Sprites/Default"));
        m.color = new Color(0.12f, 0.2f, 0.03f, 0.85f);
        tideMark.sharedMaterial = m;
        tideMark.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ------------------------------------------------------------------ the sound stays in the bathroom
    private float nextHearCheck, roomGain = 1f, roomTarget = 1f, drainBaseVol = 1f, simmerBaseVol = 0.55f;
    private Transform listener;

    /// <summary>Walls between the player and the tub mute it (it was heard all over the floor).</summary>
    private void RoomOnlyAudio()
    {
        if (Time.unscaledTime >= nextHearCheck)
        {
            nextHearCheck = Time.unscaledTime + 0.15f;
            if (listener == null) { AudioListener al = FindAnyObjectByType<AudioListener>(); if (al != null) listener = al.transform; }
            roomTarget = 1f;
            if (listener != null)
            {
                Vector3 from = listener.position, to = transform.position + Vector3.up * 0.6f;
                float dist = Vector3.Distance(from, to);
                if (dist > 12f) roomTarget = 0f;
                else
                    foreach (RaycastHit h in Physics.RaycastAll(from, (to - from).normalized, dist, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h.transform.IsChildOf(transform) || h.transform == transform) continue;
                        if (listener.root != null && h.transform.IsChildOf(listener.root)) continue;
                        if (h.collider.bounds.size.magnitude < 1.2f) continue;   // small props don't block sound
                        roomTarget = 0f; break;
                    }
            }
        }
        roomGain = Mathf.MoveTowards(roomGain, roomTarget, Time.unscaledDeltaTime * 2.5f);
        if (audioSource != null) audioSource.volume = drainBaseVol * roomGain;
        if (simmer != null && simmer.isPlaying) simmer.volume = simmerBaseVol * roomGain;
    }

    private Vector2 SurfaceSize()
    {
        Renderer r = acidMesh.GetComponent<Renderer>();
        return r != null ? new Vector2(r.bounds.size.x, r.bounds.size.z) : new Vector2(0.8f, 1.5f);
    }

    void Update()
    {
        if (!fxReady) return;
        RoomOnlyAudio();
        if (isSolved) return;
        UpdateTubSurface(0f, 0f);
        // the surface heaves a little and the glow breathes
        float t = Time.time;
        for (int i = 0; i < surfaces.Count; i++)
            if (surfaces[i] != null) surfaces[i].position = surfaceStart[i] + Vector3.up * (Mathf.Sin(t * 1.3f + i) * 0.003f);
        if (glow != null) glow.intensity = 1.15f + 0.25f * Mathf.PerlinNoise(t * 1.7f, 0.4f);
        foreach (Material m in acidMats) if (m != null && m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", AcidGlow * (0.45f + 0.2f * Mathf.PerlinNoise(t * 0.9f, 2.1f)));
    }

    public string GetInteractPrompt()
    {
        if (isSolved) return "";
        return "Press E to Attach Valve";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isSolved) return;

        Inventory inventory = playerStats.GetComponent<Inventory>();
        if (inventory == null)
        {
            Debug.LogError("PlayerStats does not have an Inventory component!");
            return;
        }

        if (inventory.HasItem(requiredValveItem))
        {
            isSolved = true;
            if (SaveManager.instance != null) SaveManager.instance.SetFlag(SaveFlag);
            inventory.RemoveItem(requiredValveItem, false); // Hath se nikal lo

            // Pipe par laga hua valve show kar do
            if (valveMesh != null) valveMesh.gameObject.SetActive(true);

            StartCoroutine(DrainRoutine());
        }
        else
        {
            // Agar Valve nahi hai to Hint do
            InteractionRaycast.Notify(playerStats, "The acid needs to be drained... I need a Valve handle.");
        }
    }

    // ------------------------------------------------------------------ draining

    private IEnumerator DrainRoutine()
    {
        // 1. the valve: squeal, clunk, the pipes shudder
        if (valveTurnSound != null) audioSource.PlayOneShot(valveTurnSound);
        float elapsed = 0f;
        while (elapsed < 1.2f)
        {
            elapsed += Time.deltaTime;
            if (valveMesh != null) valveMesh.Rotate(Vector3.forward * Time.deltaTime * 300f);
            yield return null;
        }

        if (acidMesh == null) { onDrainComplete?.Invoke(); yield break; }

        // 2. the plug opens: a whirlpool forms and the acid is sucked away with a long gurgle
        if (acidDrainSound != null) audioSource.PlayOneShot(acidDrainSound);
        MakeTideMark();
        Vector3 drainPoint = (outlines != null ? tubCenter : acidMesh.position) + LongAxis() * 0.28f;
        drainPoint.y = topY != 0f ? topY : acidMesh.position.y;
        Transform vortex = MakeVortex(drainPoint);
        if (fumes != null) { var em = fumes.emission; em.rateOverTimeMultiplier = 26f; }   // a burst of fumes as it churns
        float simmer0 = simmer != null ? simmer.volume : 0f;

        float dur = Mathf.Max(3f, drainDuration);
        elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float e = t * t * (3f - 2f * t);
            SetDrained(e);
            UpdateTubSurface(e, Mathf.Sin(Mathf.Clamp01(t * 1.25f) * Mathf.PI) * 0.035f * (1f - e));
            if (vortex != null)
            {
                vortex.position = new Vector3(drainPoint.x, Mathf.Lerp(drainPoint.y, bottomY, e) + 0.004f, drainPoint.z);
                vortex.Rotate(Vector3.up, Time.deltaTime * (240f + 380f * e), Space.World);
                float vs = Mathf.Sin(Mathf.Clamp01(t * 1.25f) * Mathf.PI) * 0.34f + 0.02f;
                vortex.localScale = new Vector3(vs, 1f, vs);
            }
            if (fumes != null)
            {
                fumes.transform.position = new Vector3(fumes.transform.position.x, Mathf.Lerp(surfaceStart[0].y, bottomY, e), fumes.transform.position.z);
                var em = fumes.emission; em.rateOverTimeMultiplier = Mathf.Lerp(26f, 4f, e);
            }
            if (glow != null)
            {
                glow.intensity = Mathf.Lerp(1.6f, 0.25f, e) + Random.Range(-0.08f, 0.08f);
                glow.transform.position = new Vector3(glow.transform.position.x, Mathf.Lerp(surfaceStart[0].y, bottomY, e) + 0.4f, glow.transform.position.z);
            }
            if (simmer != null) simmer.volume = Mathf.Lerp(simmer0, 0f, e);
            yield return null;
        }
        if (vortex != null) Destroy(vortex.gameObject);
        if (simmer != null) simmer.Stop();
        ResidueLook();

        // 3. Sab complete hone par Key ko accessible banao!
        onDrainComplete?.Invoke();
    }

    /// <summary>0 = full, 1 = drained to the bottom of the tub.</summary>
    private void SetDrained(float k)
    {
        for (int i = 0; i < surfaces.Count; i++)
        {
            Transform s = surfaces[i];
            if (s == null) continue;
            Vector3 p = surfaceStart[i];
            s.position = new Vector3(p.x, Mathf.Lerp(p.y, bottomY + i * 0.002f, k), p.z);
            // the tub narrows towards the bottom
            float shrink = Mathf.Lerp(1f, 0.86f, k);
            s.localScale = new Vector3(surfaceScale[i].x * shrink, surfaceScale[i].y, surfaceScale[i].z * Mathf.Lerp(1f, 0.92f, k));
        }
    }

    /// <summary>What is left: a thin, dull, faintly glowing film on the bottom that still smokes a little.</summary>
    private void ResidueLook()
    {
        UpdateTubSurface(1f, 0f);
        MakeTideMark();
        foreach (Material m in acidMats)
        {
            if (m == null) continue;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.1f, 0.25f, 0.04f, m.GetColor("_BaseColor").a));
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", AcidGlow * 0.12f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.6f);
        }
        if (glow != null) glow.intensity = 0.25f;
        if (fumes != null)
        {
            fumes.transform.position = new Vector3(fumes.transform.position.x, bottomY, fumes.transform.position.z);
            var em = fumes.emission; em.rateOverTimeMultiplier = 3f;
        }
        foreach (Transform s in surfaces)
            if (s != null)
                foreach (ParticleSystem ps in s.GetComponentsInChildren<ParticleSystem>()) { var em = ps.emission; em.rateOverTimeMultiplier *= 0.2f; }
    }

    private Vector3 LongAxis()
    {
        Vector2 sz = SurfaceSize();
        return sz.x >= sz.y ? Vector3.right : Vector3.forward;
    }

    // ------------------------------------------------------------------ procedural effects

    private static Texture2D soft;
    private static Texture2D Soft()
    {
        if (soft != null) return soft;
        soft = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = new Vector2(x - 31.5f, y - 31.5f).magnitude / 31.5f;
                float a = Mathf.Clamp01(1f - d); a = a * a * (0.75f + 0.25f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f));
                soft.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        soft.Apply();
        return soft;
    }

    private ParticleSystem MakeFumes(Vector3 pos, Vector2 size)
    {
        GameObject g = new GameObject("AcidFumes");
        g.transform.SetParent(transform, true);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.LookRotation(Vector3.up);
        ParticleSystem ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 1f, 0.35f, 0.16f), new Color(0.8f, 1f, 0.7f, 0.08f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        var em = ps.emission; em.rateOverTime = 7f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(size.x * 0.8f, size.y * 0.8f, 0.02f);
        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.12f; noise.frequency = 0.4f;
        ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = Soft() };
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    /// <summary>A dark spiral that spins over the plug hole.</summary>
    private Transform MakeVortex(Vector3 pos)
    {
        GameObject g = new GameObject("AcidWhirlpool");
        g.transform.SetParent(transform, true);
        g.transform.position = pos + Vector3.up * 0.004f;
        int arms = 4, seg = 28;
        List<Vector3> v = new List<Vector3>(); List<int> tri = new List<int>(); List<Color> c = new List<Color>();
        for (int a = 0; a < arms; a++)
        {
            float off = a * Mathf.PI * 2f / arms;
            int baseIdx = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float k = i / (float)seg;
                float ang = off + k * Mathf.PI * 2.4f;
                float r = Mathf.Lerp(0.03f, 0.5f, k);
                float w = Mathf.Lerp(0.035f, 0.11f, k);
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                v.Add(dir * (r - w * 0.5f)); v.Add(dir * (r + w * 0.5f));
                float alpha = (1f - k) * 0.85f;
                c.Add(new Color(0.02f, 0.08f, 0.01f, alpha)); c.Add(new Color(0.02f, 0.08f, 0.01f, alpha));
                if (i < seg)
                {
                    int q = baseIdx + i * 2;
                    tri.Add(q); tri.Add(q + 2); tri.Add(q + 1);
                    tri.Add(q + 1); tri.Add(q + 2); tri.Add(q + 3);
                }
            }
        }
        // the dark hole in the middle
        int center = v.Count; v.Add(Vector3.zero); c.Add(new Color(0f, 0.02f, 0f, 0.95f));
        for (int i = 0; i < 20; i++)
        {
            float ang = i * Mathf.PI * 2f / 20f;
            v.Add(new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.09f); c.Add(new Color(0f, 0.03f, 0f, 0f));
        }
        for (int i = 0; i < 20; i++) { tri.Add(center); tri.Add(center + 1 + (i + 1) % 20); tri.Add(center + 1 + i); }
        Mesh m = new Mesh();
        m.SetVertices(v); m.SetColors(c); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        g.AddComponent<MeshFilter>().sharedMesh = m;
        MeshRenderer mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.transform.localScale = new Vector3(0.02f, 1f, 0.02f);
        return g.transform;
    }
}
