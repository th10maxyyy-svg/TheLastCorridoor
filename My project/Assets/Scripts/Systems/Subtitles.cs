using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SUBTITLES for spoken lines. Shown at the bottom of the screen by TLCHud (TLC_UI / Subtitles).
/// Lines are matched by the AUDIO CLIP NAME and read from Assets/Resources/Subtitles.txt, one per line:
///     ClipName | Text on screen | seconds (optional)
/// To subtitle a new voice clip just add a line to that file - no code needed.
/// (In the Editor a clip that has no subtitle yet is reported once in the Console.)
/// </summary>
public static class Subtitles
{
    public static string Current { get; private set; } = "";
    public static float Until { get; private set; }
    public static float StartedAt { get; private set; }

    private static Dictionary<string, KeyValuePair<string, float>> lines;
    private static readonly HashSet<string> reported = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Current = ""; Until = 0f; lines = null; reported.Clear(); }

    public static void Show(string text, float seconds)
    {
        if (string.IsNullOrEmpty(text)) return;
        Current = text;
        StartedAt = Time.unscaledTime;
        Until = Time.unscaledTime + Mathf.Max(1.2f, seconds);
    }

    public static void Clear() { Until = 0f; }

    /// <summary>Shows the subtitle that belongs to this voice clip (if there is one).</summary>
    public static void ForClip(AudioClip clip)
    {
        if (clip == null) return;
        Load();
        if (lines.TryGetValue(clip.name.Trim().ToLowerInvariant(), out KeyValuePair<string, float> l))
        {
            Show(l.Key, l.Value > 0f ? l.Value : Mathf.Clamp(clip.length, 1.5f, 7f));
            return;
        }
#if UNITY_EDITOR
        if (reported.Add(clip.name))
            Debug.Log("[TLC] Subtitles: no subtitle for voice clip '" + clip.name + "'. Add a line  " + clip.name + " | text  to Assets/Resources/Subtitles.txt");
#endif
    }

    private static void Load()
    {
        if (lines != null) return;
        lines = new Dictionary<string, KeyValuePair<string, float>>();
        TextAsset ta = Resources.Load<TextAsset>("Subtitles");
        if (ta == null) return;
        foreach (string raw in ta.text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;
            string[] p = line.Split('|');
            if (p.Length < 2) continue;
            float secs = 0f;
            if (p.Length > 2) float.TryParse(p[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out secs);
            lines[p[0].Trim().ToLowerInvariant()] = new KeyValuePair<string, float>(p[1].Trim(), secs);
        }
    }
}
