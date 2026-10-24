using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SMALL ONE-TIME SCARES. The first time the player opens certain room doors, something happens a moment later:
///   Stock Room       - a door somewhere behind him slams shut
///   Men's Toilets    - the lights die for a few seconds, a bulb bursts, whispers
///   Exam Room        - a laugh far behind him in the corridor
///   Office (ground)  - someone is singing inside the room
///   Women's Toilets  - a breath right behind his neck
///   Gatehouse        - lights flicker, whispers
/// Each scare happens once per game (saved), never while the clown is chasing or close, and at most one every 75 s.
/// Created automatically in the game level by GameUI.
/// </summary>
public class ScareDirector : MonoBehaviour
{
    private enum Kind { DoorSlam, LightsOut, FarLaugh, Singing, Breath, Flicker }
    private static readonly Dictionary<string, Kind> ByRoom = new Dictionary<string, Kind>
    {
        { "Stock Room", Kind.DoorSlam }, { "Men's Toilets", Kind.LightsOut }, { "Exam Room", Kind.FarLaugh },
        { "Office", Kind.Singing }, { "Women's Toilets", Kind.Breath }, { "Gatehouse", Kind.Flicker },
    };

    private float lastScare = -999f;
    private AudioClip whispers, singing, bulb, creak, laugh, breath;
    private PlayerController player;
    private EnemyAI enemy;

    void OnEnable() { DoorController.PlayerOpened += OnDoorOpened; }
    void OnDisable() { DoorController.PlayerOpened -= OnDoorOpened; }

    void Start()
    {
        whispers = Resources.Load<AudioClip>("HorrorAudio/TLC_Scare_Whispers");
        singing = Resources.Load<AudioClip>("HorrorAudio/TLC_Scare_Singing");
        bulb = Resources.Load<AudioClip>("HorrorAudio/TLC_Scare_Bulb");
        creak = Resources.Load<AudioClip>("HorrorAudio/TLC_Scare_DoorCreak");
        laugh = Resources.Load<AudioClip>("HorrorAudio/oliper18-scary-laugh-377526");
        breath = Resources.Load<AudioClip>("HorrorAudio/freesound_community-breath-stinger1-96164");
    }

    private void OnDoorOpened(DoorController door)
    {
        if (door == null || string.IsNullOrEmpty(door.RoomName)) return;
        if (!ByRoom.TryGetValue(door.RoomName, out Kind kind)) return;
        string flag = "Scare_" + door.RoomName.Replace(" ", "").Replace("'", "");
        SaveManager sm = SaveManager.instance;
        if (sm != null && sm.HasFlag(flag)) return;
        if (Time.time - lastScare < 75f) return;
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (enemy == null) enemy = FindAnyObjectByType<EnemyAI>();
        if (player == null || player.isCinematicPlaying) return;
        if (enemy != null && enemy.isActiveAndEnabled &&
            (enemy.State == EnemyAI.AIState.Chase || Vector3.Distance(enemy.transform.position, player.transform.position) < 10f)) return;

        lastScare = Time.time;
        if (sm != null) sm.SetFlag(flag);
        StartCoroutine(Play(kind, door));
    }

    private IEnumerator Play(Kind kind, DoorController opened)
    {
        yield return new WaitForSeconds(Random.Range(1.2f, 2.2f));
        if (player == null) yield break;
        Transform p = player.transform;
        switch (kind)
        {
            case Kind.DoorSlam:
            {
                DoorController best = null; float bestD = 99f;
                foreach (DoorController d in FindObjectsByType<DoorController>())
                {
                    if (d == opened || !d.IsOpen || d.IsLocked || string.IsNullOrEmpty(d.RoomName)) continue;
                    float dist = Vector3.Distance(d.transform.position, p.position);
                    if (dist > 4f && dist < 18f && dist < bestD) { best = d; bestD = dist; }
                }
                if (best != null) best.SlamShut();
                else PlayAt(creak, p.position - p.forward * 8f, 1f, 0.9f);
                break;
            }
            case Kind.LightsOut:
                PlayAt(bulb, p.position + p.forward * 3f + Vector3.up * 2f, 1f, 1f);
                yield return Lights(p.position, 10f, 3.2f, false);
                PlayAt(whispers, p.position - p.forward * 2f, 0.9f, 1f);
                break;
            case Kind.Flicker:
                PlayAt(whispers, p.position + p.right * 2f, 0.8f, 1f);
                yield return Lights(p.position, 10f, 2.2f, true);
                break;
            case Kind.FarLaugh:
                PlayAt(laugh, p.position - p.forward * 14f, 1f, 0.95f, 25f);
                break;
            case Kind.Singing:
                PlayAt(singing, p.position + p.forward * 4f, 0.8f, 1f, 14f);
                break;
            case Kind.Breath:
                PlayAt(breath, p.position - p.forward * 0.6f + Vector3.up * 0.2f, 1f, 1f, 5f);
                break;
        }
    }

    // lights around the player go out (or flicker), then come back exactly as they were
    private IEnumerator Lights(Vector3 at, float radius, float seconds, bool flicker)
    {
        List<Light> ls = new List<Light>();
        foreach (Light l in FindObjectsByType<Light>())
        {
            if (!l.enabled || l.type == LightType.Directional) continue;
            if (player != null && l.transform.IsChildOf(player.transform)) continue;      // never the headlamp
            if (Vector3.Distance(l.transform.position, at) < radius) ls.Add(l);
        }
        float end = Time.time + seconds;
        while (Time.time < end)
        {
            bool on = flicker ? Random.value > 0.55f : false;
            foreach (Light l in ls) if (l != null) l.enabled = on;
            yield return new WaitForSeconds(flicker ? Random.Range(0.05f, 0.18f) : seconds);
        }
        foreach (Light l in ls) if (l != null) l.enabled = true;
    }

    private static void PlayAt(AudioClip clip, Vector3 pos, float volume, float spatial, float maxDist = 20f)
    {
        if (clip == null) return;
        GameObject g = new GameObject("ScareSound");
        g.transform.position = pos;
        AudioSource s = g.AddComponent<AudioSource>();
        s.clip = clip; s.volume = volume; s.spatialBlend = spatial;
        s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 2f; s.maxDistance = maxDist;
        s.Play();
        Object.Destroy(g, clip.length + 0.2f);
    }
}
