using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Studio intro: black screen, heartbeat line, "HOLLOWVEIN" appears letter by letter (VEIN beats like a heart),
/// "STUDIOS", "PRODUCTIONS PRESENTS", fade out, then the main menu scene loads. Any key / click skips it.
/// Everything it animates is a normal object of the Intro scene (see the Hierarchy: IntroCanvas).
/// </summary>
public class IntroSequence : MonoBehaviour
{
    [Header("Next scene")]
    public string nextScene = "main menue";

    [Header("Scene objects (assigned automatically by the setup script)")]
    public CanvasGroup content;              // everything except the black background / vignette / fade
    public Image glow;
    public IntroEcgLine ecg;
    public IntroEcgLine ecgGlow;
    public List<Text> letters = new List<Text>();     // H O L L O W V E I N
    public int veinFirstIndex = 6;
    public RectTransform veinWord;
    public Text studios;
    public RectTransform flankLeft, flankRight;
    public Text presents;
    public Image fadeOverlay;
    public AudioSource audioSource;
    public AudioClip heartbeat;

    [Header("Timing (seconds)")]
    public float totalLength = 7.4f;
    public bool allowSkip = true;

    private static readonly float[] Beats = { 1.9f, 3.3f, 4.4f, 5.4f };
    private static readonly Color Bone = new Color(0.91f, 0.88f, 0.84f, 1f);
    private static readonly Color Blood = new Color(0.77f, 0.157f, 0.10f, 1f);

    private AnimationCurve glowCurve, beatCurve;
    private float t;
    private float skipT = -1f;
    private bool loading;

    void Awake()
    {
        // glow brightness and the "heart beat" scale of VEIN (lub-dub at every beat)
        List<Keyframe> g = new List<Keyframe> { new Keyframe(0f, 0f), new Keyframe(0.5f, 0f) };
        List<Keyframe> s = new List<Keyframe> { new Keyframe(0f, 1f) };
        foreach (float b in Beats)
        {
            g.Add(new Keyframe(b - 0.05f, 0.18f)); g.Add(new Keyframe(b + 0.08f, 0.85f)); g.Add(new Keyframe(b + 0.25f, 0.30f));
            g.Add(new Keyframe(b + 0.40f, 0.60f)); g.Add(new Keyframe(b + 0.70f, 0.22f));
            if (b > Beats[0])
            {
                s.Add(new Keyframe(b - 0.02f, 1f)); s.Add(new Keyframe(b + 0.08f, 1.045f)); s.Add(new Keyframe(b + 0.20f, 1f));
                s.Add(new Keyframe(b + 0.30f, 1.03f)); s.Add(new Keyframe(b + 0.45f, 1f));
            }
        }
        g.Add(new Keyframe(6.9f, 0f));
        glowCurve = Linear(g); beatCurve = Linear(s);
    }

    private static AnimationCurve Linear(List<Keyframe> keys)
    {
        AnimationCurve c = new AnimationCurve(keys.ToArray());
        for (int i = 0; i < c.length; i++) { AnimationUtility_SetLinear(c, i); }
        return c;
    }

    // linear tangents at runtime (AnimationUtility is editor-only)
    private static void AnimationUtility_SetLinear(AnimationCurve c, int i)
    {
        Keyframe k = c[i];
        float inT = 0f, outT = 0f;
        if (i > 0) { Keyframe p = c[i - 1]; inT = (k.value - p.value) / Mathf.Max(1e-4f, k.time - p.time); }
        if (i < c.length - 1) { Keyframe n = c[i + 1]; outT = (n.value - k.value) / Mathf.Max(1e-4f, n.time - k.time); }
        k.inTangent = inT; k.outTangent = outT;
        c.MoveKey(i, k);
    }

    void Start()
    {
        Cursor.visible = true;
        Time.timeScale = 1f;
        if (audioSource != null && heartbeat != null) { audioSource.clip = heartbeat; audioSource.Play(); }
        Apply(0f);
    }

    void Update()
    {
        if (loading) return;
        // the first frames of a scene can take seconds (loading, play-mode start): never count more than 0.05 s per frame
        t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        if (allowSkip && skipT < 0f && t > 1.0f && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
        {
            skipT = t;
            Debug.Log("Intro: skipped by the player at " + t.ToString("0.0") + " s");
        }

        Apply(t);

        float fade = 0f;
        if (skipT >= 0f) fade = Mathf.Clamp01((t - skipT) / 0.35f);
        if (fadeOverlay != null) { Color c = fadeOverlay.color; c.a = fade; fadeOverlay.color = c; }
        if (audioSource != null && skipT >= 0f) audioSource.volume = 1f - fade;

        if ((skipT >= 0f && fade >= 1f) || t >= totalLength) Load();
    }

    private static float Ramp(float x, float a, float b) { return Mathf.Clamp01((x - a) / Mathf.Max(1e-4f, b - a)); }

    private void Apply(float time)
    {
        float master = 1f - Ramp(time, 6.3f, 6.9f);
        if (content != null) content.alpha = master;

        if (glow != null) { Color c = glow.color; c.a = glowCurve.Evaluate(time) * 0.9f; glow.color = c; }

        float ecgAlpha = 1f - Ramp(time, 2.7f, 3.1f);
        float prog = Ramp(time, 1.0f, 2.7f);
        if (ecg != null) { ecg.SetProgress(prog); SetAlpha(ecg, ecgAlpha); }
        if (ecgGlow != null) { ecgGlow.SetProgress(prog); SetAlpha(ecgGlow, ecgAlpha * 0.28f); }

        for (int i = 0; i < letters.Count; i++)
        {
            Text l = letters[i];
            if (l == null) continue;
            bool vein = i >= veinFirstIndex;
            float start = vein ? 3.3f + 0.12f * (i - veinFirstIndex) : 2.7f + 0.10f * i;
            float a = Ramp(time, start, start + 0.18f);
            float settle = Ramp(time, start + 0.18f, start + 0.55f);
            Color baseC = vein ? Blood : Bone;
            Color flash = vein ? new Color(1f, 0.62f, 0.45f, 1f) : Color.white;
            Color c = Color.Lerp(flash, baseC, settle);
            c.a = a;
            l.color = c;
            l.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.14f, 1f, a);
        }
        if (veinWord != null) veinWord.localScale = Vector3.one * beatCurve.Evaluate(time);

        float st = Ramp(time, 4.0f, 4.9f);
        if (studios != null)
        {
            Color c = studios.color; c.a = st; studios.color = c;
            studios.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.93f, 1f, st);
        }
        float fl = Ramp(time, 4.0f, 4.8f);
        if (flankLeft != null) flankLeft.localScale = new Vector3(fl, 1f, 1f);
        if (flankRight != null) flankRight.localScale = new Vector3(fl, 1f, 1f);
        if (presents != null) { Color c = presents.color; c.a = Ramp(time, 5.0f, 5.8f); presents.color = c; }
    }

    private static void SetAlpha(Graphic g, float a) { Color c = g.color; c.a = a; g.color = c; }

    private void Load()
    {
        if (loading) return;
        loading = true;
        Debug.Log("Intro: loading '" + nextScene + "' at " + t.ToString("0.0") + " s");
        SceneLoader.Load(nextScene);
    }
}
