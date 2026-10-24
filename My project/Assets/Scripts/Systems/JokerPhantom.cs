using System.Collections;
using UnityEngine;

/// <summary>
/// A HALLUCINATION of the clown (toxic gas): a copy of the real, animated clown model - no AI, no collider, no sounds of
/// its own - that the player sees for a moment. It is never a stiff "puppet": it roars, lunges, tilts its head, flickers.
///   Lunge   - appears a few metres ahead in the gas, roars, then rushes at the player's face and is gone.
///   Behind  - stands close at the edge of view, staring, laughing; the moment it is looked at, it flickers out.
///   Far     - stands far off in the fog, staring, then twitches and vanishes.
/// </summary>
public class JokerPhantom : MonoBehaviour
{
    public enum Kind { Lunge, Behind, Far }

    private Animator anim;
    private Renderer[] rends;

    /// <summary>Makes the phantom (or returns null if there is no clown model to copy).</summary>
    public static JokerPhantom Create(GameObject fallbackPrefab)
    {
        GameObject src = null;
        EnemyAI enemy = FindAnyObjectByType<EnemyAI>(FindObjectsInactive.Include);
        if (enemy != null) src = enemy.gameObject;
        if (src == null && fallbackPrefab == null) return null;

        // copy it under an inactive holder so none of its scripts wake up, strip everything but the look and the Animator
        GameObject holder = new GameObject("PhantomHolder");
        holder.SetActive(false);
        GameObject g = Instantiate(src != null ? src : fallbackPrefab, holder.transform);
        g.name = "JokerHallucination";
        // scripts (newest first, a few passes - one script may need another)
        for (int pass = 0; pass < 3; pass++)
        {
            MonoBehaviour[] mbs = g.GetComponentsInChildren<MonoBehaviour>(true);
            if (mbs.Length == 0) break;
            for (int i = mbs.Length - 1; i >= 0; i--) if (mbs[i] != null && CanRemove(mbs[i])) DestroyImmediate(mbs[i]);
        }
        foreach (UnityEngine.AI.NavMeshAgent a in g.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) if (CanRemove(a)) DestroyImmediate(a);
        foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (Rigidbody r in g.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(r);
        foreach (AudioSource s in g.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(s);
        foreach (Light l in g.GetComponentsInChildren<Light>(true)) DestroyImmediate(l);
        g.transform.SetParent(null, true);
        Destroy(holder);
        g.SetActive(true);

        JokerPhantom p = g.AddComponent<JokerPhantom>();
        p.anim = g.GetComponentInChildren<Animator>();
        if (p.anim != null) { p.anim.applyRootMotion = false; p.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        p.rends = g.GetComponentsInChildren<Renderer>(true);
        bool anyOn = false; foreach (Renderer r in p.rends) if (r.enabled) anyOn = true;
        if (!anyOn) foreach (Renderer r in p.rends) r.enabled = true;   // the real clown was hidden at that moment
        System.Collections.Generic.List<Renderer> on = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in p.rends) if (r.enabled) on.Add(r);
        p.rends = on.ToArray();
        return p;
    }

    /// <summary>No other component on the object says it needs this one.</summary>
    private static bool CanRemove(Component c)
    {
        foreach (Component o in c.GetComponents<Component>())
        {
            if (o == null || o == c || o is Transform) continue;
            foreach (object at in o.GetType().GetCustomAttributes(typeof(RequireComponent), true))
            {
                RequireComponent rc = (RequireComponent)at;
                System.Type t = c.GetType();
                if ((rc.m_Type0 != null && rc.m_Type0.IsAssignableFrom(t)) || (rc.m_Type1 != null && rc.m_Type1.IsAssignableFrom(t)) || (rc.m_Type2 != null && rc.m_Type2.IsAssignableFrom(t)))
                    return false;
            }
        }
        return true;
    }

    private void Speed(float s) { if (anim != null) anim.SetFloat("Speed", s); }
    private void Trigger(string t) { if (anim != null) { anim.ResetTrigger("Tease"); anim.ResetTrigger("Attack"); anim.SetTrigger(t); } }
    private void Show(bool on) { foreach (Renderer r in rends) if (r != null) r.enabled = on; }

    private static void Play(AudioClip c, Vector3 at, float vol, float spatial)
    {
        if (c == null) return;
        GameObject g = new GameObject("PhantomSfx");
        g.transform.position = at;
        AudioSource a = g.AddComponent<AudioSource>();
        a.clip = c; a.volume = vol; a.spatialBlend = spatial; a.minDistance = 2f; a.maxDistance = 30f; a.priority = 20;
        a.Play();
        Destroy(g, c.length + 0.1f);
    }

    private static Vector3 Ground(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out RaycastHit h, 4f, ~0, QueryTriggerInteraction.Ignore)) return h.point;
        return p;
    }

    private static bool Clear(Vector3 eye, Vector3 at)
    {
        return !Physics.Linecast(eye, at + Vector3.up * 1.4f, ~0, QueryTriggerInteraction.Ignore);
    }

    /// <summary>Runs one hallucination and destroys itself. Returns false if there was no room for it.</summary>
    public static bool Run(MonoBehaviour host, Camera cam, Kind kind, GameObject fallbackPrefab, AudioClip extraSound)
    {
        if (cam == null) return false;
        Transform ct = cam.transform;
        Vector3 eye = ct.position;
        Vector3 fwd = Vector3.ProjectOnPlane(ct.forward, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        Vector3 pos;
        switch (kind)
        {
            case Kind.Lunge: pos = eye + fwd * 4.2f + right * Random.Range(-0.8f, 0.8f); break;
            case Kind.Behind: pos = eye + fwd * 1.6f + right * (Random.value < 0.5f ? -1.6f : 1.6f); break;
            default: pos = eye + fwd * 7.5f + right * Random.Range(-1.5f, 1.5f); break;
        }
        pos = Ground(new Vector3(pos.x, eye.y - 1.5f, pos.z));
        if (!Clear(eye, pos))
        {
            // try a little closer
            Vector3 p2 = Ground(Vector3.Lerp(new Vector3(eye.x, pos.y, eye.z), pos, 0.55f));
            if (!Clear(eye, p2)) return false;
            pos = p2;
        }
        JokerPhantom ph = Create(fallbackPrefab);
        if (ph == null) return false;
        ph.transform.position = pos;
        ph.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(eye - pos, Vector3.up), Vector3.up);
        host.StartCoroutine(ph.Act(kind, cam, extraSound));
        return true;
    }

    private IEnumerator Act(Kind kind, Camera cam, AudioClip extra)
    {
        Transform ct = cam.transform;
        AudioClip laugh = Resources.Load<AudioClip>("HorrorAudio/joker-2008-laugh");
        AudioClip shriek = Resources.Load<AudioClip>("HorrorAudio/Joker_BurnShriek");
        AudioClip hiss = Resources.Load<AudioClip>("HorrorAudio/Joker_FleeHiss");
        Speed(0f);

        if (kind == Kind.Lunge)
        {
            // flickers into existence, roars...
            Trigger("Tease");
            Play(extra != null ? extra : hiss, transform.position + Vector3.up * 1.5f, 0.9f, 0.8f);
            for (int i = 0; i < 4; i++) { Show(i % 2 == 0); yield return new WaitForSeconds(Random.Range(0.04f, 0.09f)); }
            Show(true);
            float t = 0f;
            while (t < 0.75f) { t += Time.deltaTime; Face(ct); yield return null; }
            // ...and comes at the face
            Play(shriek, ct.position, 1f, 0f);
            Trigger("Attack");
            Speed(7f);
            Vector3 from = transform.position;
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.42f;
                Vector3 to = ct.position + Vector3.ProjectOnPlane(ct.forward, Vector3.up).normalized * 0.55f;
                to.y = from.y;
                transform.position = Vector3.Lerp(from, to, t * t);
                Face(ct);
                yield return null;
            }
            yield return ShakeAndFlash(ct, 0.35f);
        }
        else if (kind == Kind.Behind)
        {
            Play(laugh, transform.position + Vector3.up * 1.6f, 0.75f, 1f);
            float t = 0f, seen = 0f;
            while (t < 4f)
            {
                t += Time.deltaTime;
                Face(ct);
                // head tilted, swaying slowly - wrong
                transform.rotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 2.1f) * 4f);
                Vector3 to = (transform.position + Vector3.up * 1.5f) - ct.position;
                if (Vector3.Dot(ct.forward, to.normalized) > 0.8f) seen += Time.deltaTime;
                if (seen > 0.25f)
                {
                    Trigger("Tease");
                    Play(shriek, ct.position, 0.8f, 0f);
                    for (int i = 0; i < 5; i++) { Show(i % 2 == 1); yield return new WaitForSeconds(0.05f); }
                    break;
                }
                yield return null;
            }
        }
        else
        {
            Play(laugh, transform.position + Vector3.up * 1.6f, 0.55f, 1f);
            float t = 0f;
            while (t < 2.6f) { t += Time.deltaTime; Face(ct); yield return null; }
            Trigger("Tease");
            for (int i = 0; i < 6; i++) { Show(i % 2 == 1); transform.position += Random.insideUnitSphere * 0.08f; yield return new WaitForSeconds(0.05f); }
        }
        Destroy(gameObject);
    }

    private void Face(Transform ct)
    {
        Vector3 d = Vector3.ProjectOnPlane(ct.position - transform.position, Vector3.up);
        if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 10f);
    }

    /// <summary>The camera jolts and the screen flashes when it hits.</summary>
    private IEnumerator ShakeAndFlash(Transform ct, float dur)
    {
        Show(false);
        ScreenFlash.Flash(new Color(0.55f, 1f, 0.45f, 0.55f), 0.45f);
        float t = 0f;
        while (t < dur)
        {
            yield return null;
            t += Time.deltaTime;
            float k = 1f - t / dur;
            ct.localRotation *= Quaternion.Euler(Random.Range(-6f, 6f) * k, Random.Range(-6f, 6f) * k, Random.Range(-3f, 3f) * k);
        }
    }
}

/// <summary>Full-screen colour flash / blackout drawn over everything (no UI setup needed).</summary>
public class ScreenFlash : MonoBehaviour
{
    private static ScreenFlash inst;
    private Color color;
    private float alpha, fadeSpeed, hold;
    private static Texture2D white;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { inst = null; }

    private static ScreenFlash Get()
    {
        if (inst == null)
        {
            GameObject g = new GameObject("ScreenFlash");
            inst = g.AddComponent<ScreenFlash>();
        }
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        return inst;
    }

    /// <summary>Instant flash that fades out over 'fade' seconds.</summary>
    public static void Flash(Color c, float fade) { ScreenFlash s = Get(); s.color = c; s.alpha = c.a; s.hold = 0f; s.fadeSpeed = c.a / Mathf.Max(0.01f, fade); }

    /// <summary>Black screen: on at once, held 'hold' seconds, then fades back over 'fade' seconds.</summary>
    public static void Blackout(float hold, float fade) { ScreenFlash s = Get(); s.color = Color.black; s.alpha = 1f; s.hold = hold; s.fadeSpeed = 1f / Mathf.Max(0.01f, fade); }

    void Update()
    {
        if (hold > 0f) { hold -= Time.unscaledDeltaTime; return; }
        alpha = Mathf.MoveTowards(alpha, 0f, fadeSpeed * Time.unscaledDeltaTime);
    }

    void OnGUI()
    {
        if (alpha <= 0.001f || white == null) return;
        GUI.depth = -1000;
        Color old = GUI.color;
        GUI.color = new Color(color.r, color.g, color.b, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
        GUI.color = old;
    }
}

/// <summary>Runs a coroutine on an object that outlives the caller (e.g. a statue that destroys itself).</summary>
public class FxRunner : MonoBehaviour
{
    private static FxRunner inst;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { inst = null; }
    public static Coroutine Run(IEnumerator routine)
    {
        if (inst == null) inst = new GameObject("FxRunner").AddComponent<FxRunner>();
        return inst.StartCoroutine(routine);
    }
}
