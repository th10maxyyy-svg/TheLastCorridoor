using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player talking to himself (English voice lines with subtitles), one line after the other.
///   PlayerMonologue.Say(delay, "Clip_Name|Subtitle text", "Clip_Name2|Second line");
/// Clips are loaded from Resources/HorrorAudio. A line whose clip is missing is still shown as a subtitle.
/// </summary>
public class PlayerMonologue : MonoBehaviour
{
    private static PlayerMonologue inst;
    private readonly Queue<KeyValuePair<float, string>> queue = new Queue<KeyValuePair<float, string>>();
    private AudioSource src;
    private bool running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { inst = null; }

    public static void Say(float delay, params string[] lines)
    {
        if (inst == null)
        {
            GameObject g = new GameObject("PlayerMonologue");
            inst = g.AddComponent<PlayerMonologue>();
            inst.src = g.AddComponent<AudioSource>();
            inst.src.spatialBlend = 0f; inst.src.playOnAwake = false; inst.src.priority = 20; inst.src.volume = 1f;
        }
        for (int i = 0; i < lines.Length; i++) inst.queue.Enqueue(new KeyValuePair<float, string>(i == 0 ? delay : 0.45f, lines[i]));
        if (!inst.running) inst.StartCoroutine(inst.Run());
    }

    private IEnumerator Run()
    {
        running = true;
        while (queue.Count > 0)
        {
            KeyValuePair<float, string> l = queue.Dequeue();
            if (l.Key > 0f) yield return new WaitForSeconds(l.Key);
            string[] parts = l.Value.Split('|');
            AudioClip clip = Resources.Load<AudioClip>("HorrorAudio/" + parts[0].Trim());
            string text = parts.Length > 1 ? parts[1].Trim() : "";
            float len = clip != null ? clip.length : Mathf.Max(2f, text.Length * 0.06f);
            if (clip != null) src.PlayOneShot(clip);
            Subtitles.Show(text, len + 0.4f);
            yield return new WaitForSeconds(len);
        }
        running = false;
    }
}
