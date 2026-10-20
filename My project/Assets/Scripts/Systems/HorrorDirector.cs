using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// HORROR DIRECTOR (FDS Section 6.2 - FR_11, FR_12, FR_13) + SESSION VARIATION (FR_08) + ENEMY RESTORE (FR_40).
///
///  - Unscripted horror events: lights flicker, knocks / whispers / creaks with no visible source, loose objects get pushed.
///    WHERE and WHEN they happen is random - they are not placed on a timeline.
///  - The more tension (enemy chasing / searching / close, low health, high aggression), the more often they happen (FR_12).
///  - If the player stands still too long, the asylum reacts to it (FR_13).
///  - Every new game rolls a seed that changes the corridor lighting (some lights brighter/dimmer, warmer/colder,
///    a few dead) - same seed after a death or reload, a new one in the next game (FR_08).
///  - On Resume / Load the enemy is put back where he was at the last safe checkpoint (FR_40).
///
/// IMPORTANT: everything here is COSMETIC. The enemy AI never hears these events and no puzzle, door or item is touched,
/// so the game logic stays exactly the same. Created automatically by GameUI in the game level.
/// </summary>
public class HorrorDirector : MonoBehaviour
{
    [Header("Event timing (seconds)")]
    public float calmInterval = 85f;       // between events when nothing is happening
    public float tenseInterval = 22f;      // between events at maximum tension
    public float stillnessTime = 30f;      // standing still this long makes the asylum react

    [Header("Ranges (meters)")]
    public float lightRange = 14f;
    public float soundMinDistance = 6f;
    public float soundMaxDistance = 13f;
    public float pushRange = 10f;

    public float Tension { get; private set; }

    private PlayerController player;
    private PlayerStats stats;
    private EnemyAI enemy;
    private Camera cam;
    private float nextEventTime;
    private float stillTimer;
    private bool busy;
    private System.Random rng;

    private static AudioClip knockClip, whisperClip, creakClip, thudClip;

    void Start()
    {
        rng = new System.Random();
        nextEventTime = Time.time + 45f; // let the player settle in first
        StartCoroutine(StartupRoutine());
    }

    private IEnumerator StartupRoutine()
    {
        // wait until the scene has been restored by RespawnManager / ProgressionManager
        yield return null;
        yield return null;
        SessionVariation.Apply();
        yield return new WaitForSeconds(0.75f);
        EnemyRestore.TryRestore();
    }

    // ------------------------------------------------------------------------------------------ tension (FR_12)
    private void FindRefs()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (player != null && stats == null) stats = player.GetComponent<PlayerStats>();
        if (enemy == null) enemy = FindAnyObjectByType<EnemyAI>();
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
    }

    private float ComputeTension()
    {
        float t = 0f;
        if (enemy != null && enemy.isActiveAndEnabled)
        {
            switch (enemy.State)
            {
                case EnemyAI.AIState.Chase: t += 0.6f; break;
                case EnemyAI.AIState.Search:
                case EnemyAI.AIState.InvestigateSound: t += 0.35f; break;
                case EnemyAI.AIState.Patrol: t += 0.1f; break;
            }
            if (player != null && Vector3.Distance(enemy.transform.position, player.transform.position) < 15f) t += 0.2f;
            t += (enemy.AggressionLevel - 3) * 0.05f; // adaptive AI: confident players get a busier asylum
        }
        if (stats != null && stats.GetHealth() < 50f) t += 0.2f;
        return Mathf.Clamp01(t);
    }

    private bool CanRun()
    {
        if (player == null || !player.enabled || player.isDead || player.isCinematicPlaying) return false;
        if (stats != null && stats.IsDead()) return false;
        if (Time.timeScale <= 0f || KeypadController.IsAnyKeypadOpen || GameUI.IsSummaryOpen) return false;
        if (GameManager.instance != null && GameManager.instance.IsGameOver()) return false;
        return true;
    }

    void Update()
    {
        FindRefs();
        if (!CanRun() || busy) return;

        Tension = ComputeTension();

        // FR_13: the asylum reacts to inaction
        if (!player.isHidden && player.GetCurrentSpeed() < 0.2f) stillTimer += Time.deltaTime;
        else stillTimer = 0f;

        if (stillTimer >= stillnessTime)
        {
            stillTimer = 0f;
            StartCoroutine(RunEvent(true));
            return;
        }

        if (Time.time >= nextEventTime)
        {
            float interval = Mathf.Lerp(calmInterval, tenseInterval, Tension) * (0.8f + 0.4f * (float)rng.NextDouble());
            nextEventTime = Time.time + interval;
            StartCoroutine(RunEvent(false));
        }
    }

    // ------------------------------------------------------------------------------------------ events (FR_11)
    private IEnumerator RunEvent(bool reactionToStillness)
    {
        busy = true;
        int roll = rng.Next(100);
        bool done = false;

        if (reactionToStillness)
        {
            // something close by "notices" the player standing still
            if (roll < 50) done = PlaySoundNear(roll < 25 ? Whisper() : Knock(), 3f, 6f);
            else { yield return FlickerLights(player.transform.position, 8f, 2.2f); done = true; }
        }
        else if (roll < 40)
        {
            yield return FlickerLights(player.transform.position, lightRange, 1.2f + (float)rng.NextDouble());
            done = true;
        }
        else if (roll < 75)
        {
            AudioClip clip = roll < 52 ? Knock() : roll < 64 ? Whisper() : Creak();
            done = PlaySoundNear(clip, soundMinDistance, soundMaxDistance);
        }
        else
        {
            done = PushLooseObject();
        }

        if (!done) // fallback so a "failed" event still counts
        {
            yield return FlickerLights(player.transform.position, lightRange, 1.2f);
        }
        busy = false;
    }

    private static bool BelongsToGameplay(Transform t)
    {
        // never touch the player, the enemy or anything the player can interact with / that a puzzle uses
        return t.GetComponentInParent<PlayerController>() != null
            || t.GetComponentInParent<EnemyAI>() != null
            || t.GetComponentInParent<IInteractable>() != null
            || t.GetComponentInParent<ThrowableItem>() != null
            || t.GetComponentInParent<DoorController>() != null;
    }

    private IEnumerator FlickerLights(Vector3 center, float range, float duration)
    {
        List<Light> targets = new List<Light>();
        foreach (Light l in FindObjectsByType<Light>())
        {
            if (!l.isActiveAndEnabled || l.type == LightType.Directional) continue;
            if (Vector3.Distance(l.transform.position, center) > range) continue;
            if (BelongsToGameplay(l.transform)) continue;
            if (l.GetComponent<LightFlicker>() != null || l.GetComponent<FlickeringLight>() != null) continue; // already animated
            targets.Add(l);
            if (targets.Count >= 4) break;
        }
        if (targets.Count == 0) yield break;

        float end = Time.time + duration;
        bool lastSet = true;
        while (Time.time < end)
        {
            if (!CanRun()) break;
            lastSet = rng.NextDouble() > 0.45;
            foreach (Light l in targets) if (l != null) l.enabled = lastSet;
            yield return new WaitForSeconds(0.04f + (float)rng.NextDouble() * 0.16f);
        }
        // restore - but only lights still in the state WE left them (another script may have switched one meanwhile)
        foreach (Light l in targets) if (l != null && l.enabled == lastSet) l.enabled = true;
    }

    private bool PlaySoundNear(AudioClip clip, float minDist, float maxDist)
    {
        if (clip == null || player == null) return false;
        Vector3 forward = cam != null ? cam.transform.forward : player.transform.forward;
        forward.y = 0f;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            // mostly behind or beside the player - sounds you can't see the source of
            float angle = 90f + (float)rng.NextDouble() * 180f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward.normalized;
            Vector3 p = player.transform.position + dir * Mathf.Lerp(minDist, maxDist, (float)rng.NextDouble());
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                PlayAt(clip, hit.position + Vector3.up * 1.2f, 0.85f);
                return true;
            }
        }
        return false;
    }

    private static void PlayAt(AudioClip clip, Vector3 pos, float volume)
    {
        GameObject go = new GameObject("HorrorSound");
        go.transform.position = pos;
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 2f;
        src.maxDistance = 28f;
        src.volume = volume;
        src.pitch = Random.Range(0.85f, 1.1f);
        src.Play();
        Destroy(go, clip.length / Mathf.Max(0.5f, src.pitch) + 0.5f);
    }

    private bool PushLooseObject()
    {
        if (player == null) return false;
        List<Rigidbody> candidates = new List<Rigidbody>();
        foreach (Rigidbody rb in FindObjectsByType<Rigidbody>())
        {
            if (rb.isKinematic || rb.mass > 15f || !rb.gameObject.activeInHierarchy) continue;
            if (Vector3.Distance(rb.position, player.transform.position) > pushRange) continue;
            if (BelongsToGameplay(rb.transform)) continue;
            if (rb.GetComponentInParent<KeyItem>() != null) continue;
            // only objects the player is NOT looking at - he only finds out it moved
            if (cam != null)
            {
                Vector3 v = cam.WorldToViewportPoint(rb.position);
                if (v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f) continue;
            }
            candidates.Add(rb);
        }
        if (candidates.Count == 0) return false;

        Rigidbody target = candidates[rng.Next(candidates.Count)];
        Vector3 push = new Vector3((float)rng.NextDouble() - 0.5f, 0.3f, (float)rng.NextDouble() - 0.5f).normalized;
        target.AddForce(push * Mathf.Clamp(target.mass * 2.5f, 0.5f, 12f), ForceMode.Impulse);
        PlayAt(Thud(), target.position, 0.9f);
        return true;
    }

    // ------------------------------------------------------------------------------------------ procedural sounds (no asset needed)
    private static AudioClip MakeClip(string name, float seconds, System.Func<float, float, float> sample)
    {
        int rate = 22050;
        int n = Mathf.Max(1, Mathf.RoundToInt(seconds * rate));
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            data[i] = Mathf.Clamp(sample(t, t / seconds), -1f, 1f);
        }
        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Noise() => Random.value * 2f - 1f;

    private static AudioClip Knock()
    {
        if (knockClip == null)
            knockClip = MakeClip("Knock", 1.6f, (t, p) =>
            {
                float s = 0f;
                float[] hits = { 0.05f, 0.42f, 0.8f };
                foreach (float h in hits)
                {
                    float d = t - h;
                    if (d >= 0f && d < 0.25f)
                        s += (Mathf.Sin(2f * Mathf.PI * 75f * d) * 0.9f + Noise() * 0.25f) * Mathf.Exp(-d * 28f);
                }
                return s;
            });
        return knockClip;
    }

    private static AudioClip Whisper()
    {
        if (whisperClip == null)
        {
            float lp = 0f;
            whisperClip = MakeClip("Whisper", 2.4f, (t, p) =>
            {
                lp = Mathf.Lerp(lp, Noise(), 0.35f);              // soft hiss
                float syllables = Mathf.Abs(Mathf.Sin(t * 7.3f) * Mathf.Sin(t * 2.1f + 1f));
                float env = Mathf.Sin(Mathf.PI * p);
                return lp * syllables * env * 0.55f;
            });
        }
        return whisperClip;
    }

    private static AudioClip Creak()
    {
        if (creakClip == null)
            creakClip = MakeClip("Creak", 1.3f, (t, p) =>
            {
                float f = Mathf.Lerp(140f, 95f, p) + Mathf.Sin(t * 40f) * 8f;
                float saw = Mathf.Repeat(t * f, 1f) * 2f - 1f;
                float env = Mathf.Sin(Mathf.PI * p) * (0.6f + 0.4f * Mathf.Sin(t * 23f));
                return saw * env * 0.35f;
            });
        return creakClip;
    }

    private static AudioClip Thud()
    {
        if (thudClip == null)
            thudClip = MakeClip("Thud", 0.5f, (t, p) => (Mathf.Sin(2f * Mathf.PI * 55f * t) + Noise() * 0.4f) * Mathf.Exp(-t * 12f) * 0.9f);
        return thudClip;
    }
}

/// <summary>FR_08: per-run variation of the corridor lighting (seeded, so it stays the same after a death or a reload).</summary>
public static class SessionVariation
{
    /// <summary>True when one of OUR scripts sits on the light (Unity's own components like URP light data don't count).</summary>
    private static bool HasGameScript(GameObject go)
    {
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue;
            string ns = mb.GetType().Namespace;
            if (string.IsNullOrEmpty(ns) || !ns.StartsWith("UnityEngine")) return true;
        }
        return false;
    }

    /// <summary>FNV-1a: the same text always gives the same number (string.GetHashCode is not guaranteed to).</summary>
    private static int StableHash(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return (int)h;
        }
    }

    public static void Apply()
    {
        SaveManager sm = SaveManager.instance;
        if (sm == null || sm.currentSaveData == null) return;
        if (sm.currentSaveData.variationSeed == 0)
        {
            sm.currentSaveData.variationSeed = Random.Range(1, int.MaxValue);
            sm.SaveWorldState();
        }
        int seed = sm.currentSaveData.variationSeed;

        // keep the light around puzzles, notes and keypads exactly as designed
        List<Vector3> protectedSpots = new List<Vector3>();
        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>())
            if (mb is IInteractable) protectedSpots.Add(mb.transform.position);

        int dead = 0, changed = 0;
        foreach (Light l in Object.FindObjectsByType<Light>())
        {
            if (!l.isActiveAndEnabled || (l.type != LightType.Point && l.type != LightType.Spot)) continue;
            if (l.GetComponentInParent<PlayerController>() != null || l.GetComponentInParent<EnemyAI>() != null) continue;
            if (HasGameScript(l.gameObject)) continue; // scripted lights (flicker, traps...) stay untouched

            Vector3 pos = l.transform.position;
            bool nearGameplay = false;
            foreach (Vector3 p in protectedSpots) if ((p - pos).sqrMagnitude < 25f) { nearGameplay = true; break; }

            System.Random r = new System.Random(seed ^ StableHash(SaveManager.GetSceneObjectId(l.transform)));

            if (!nearGameplay && r.NextDouble() < 0.08)
            {
                l.enabled = false; // a dead bulb this run
                dead++;
                continue;
            }
            l.intensity *= 0.8f + (float)r.NextDouble() * 0.35f;
            float warm = (float)r.NextDouble() * 0.12f - 0.06f;
            l.color = new Color(Mathf.Clamp01(l.color.r + warm), l.color.g, Mathf.Clamp01(l.color.b - warm), l.color.a);
            changed++;
        }
        Debug.Log("[SessionVariation] seed " + seed + ": " + changed + " lights varied, " + dead + " dead bulbs this run.");
    }
}


/// <summary>FR_40: puts the enemy back where he was at the last safe checkpoint - only on Resume / Load, never after a death.</summary>
public static class EnemyRestore
{
    public static bool RestoreOnNextLoad = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { RestoreOnNextLoad = false; }

    public static void TryRestore()
    {
        if (!RestoreOnNextLoad) return;
        RestoreOnNextLoad = false;

        SaveManager sm = SaveManager.instance;
        if (sm == null || !sm.currentSaveData.hasEnemyPosition || !sm.currentSaveData.isNightmareActive) return;

        EnemyAI enemy = Object.FindAnyObjectByType<EnemyAI>();
        PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
        if (enemy == null || pc == null || !enemy.isActiveAndEnabled) return;
        if (enemy.State != EnemyAI.AIState.Patrol) return; // only while he is calmly patrolling after the load

        Vector3 saved = sm.GetVector3(sm.currentSaveData.enemyPosition);
        if (Vector3.Distance(saved, pc.transform.position) < 12f) return; // never drop him right next to the player

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        if (NavMesh.SamplePosition(saved, out NavMeshHit hit, 2f, NavMesh.AllAreas) && agent.Warp(hit.position))
            Debug.Log("[EnemyRestore] Enemy put back at his saved position.");
    }
}
