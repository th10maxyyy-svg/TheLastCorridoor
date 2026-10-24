using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// ENDING "CONSUMED" - real-time 3D cinematic after CONTINUE on the ending summary (third life lost).
/// Real level, real clown model + animations, in the cell where the player is thrown (the basement drop point)
/// and the corridor outside it:
///   1. black - "You are not dead."
///   2. eyes open on the cell floor - he stands in the doorway, the lamp flickers
///   3. he walks in and stands over you
///   4. he speaks his line (voice + subtitle), the camera shakes
///   5. he turns to leave... spins back and PUNCHES you: you fall, blood on the screen, through half-open eyes
///      you see him walk out, the door slams, your eyes close
///   6. corridor: he walks away while the lights die one by one
///   7. title card "CONSUMED" -> main menu
///
/// SHOTS: placed automatically from the basement drop point of the drag cinematic and the nearest door of that room.
/// To fine-tune them: menu "The Last Corridor / Create Ending Cinematic Markers" creates child objects
/// (CamCell, EnemyDoor, EnemyStand, CamCorridor, WalkEnd) under this object - move them in the Scene view and they are used.
/// Any key skips after 2 seconds. Test it with Ctrl+Shift+E.
/// </summary>
public class ConsumedEndingCinematic : MonoBehaviour
{
    [Header("Scene")]
    public string menuScene = "main menue";

    [Header("Text")]
    public string openingLine = "You are not dead.";
    [Tooltip("Subtitle shown while the clown speaks (same words as the voice clip)")]
    public string spokenLine = "\"Shhh... no more running. You stay here now. Forever... and ever.\"";
    public string endTitle = "CONSUMED";
    public string endLine = "The asylum has a new patient. He will never be discharged.";

    [Header("Voice")]
    [Tooltip("Clown's line. Empty = loaded from Resources/HorrorAudio/Joker_You_Stay_Here")]
    public AudioClip voiceClip;
    [Range(0f, 1f)] public float voiceVolume = 1f;

    public static bool IsPlaying { get; private set; }

    // ------------------------------------------------------------------ shot layout
    public struct Shots
    {
        public bool ok;
        public Vector3 camCell, enemyDoor, enemyStand, camCorridor, walkEnd, cellCenter, doorCenter;
        public DoorController door;
        public string why;
    }

    /// <summary>Works out where every shot goes, from the basement drop point and the nearest door of that room.</summary>
    public static Shots Compute()
    {
        Shots s = new Shots();
        Transform drop = null;
        foreach (CinematicAmbush a in FindObjectsByType<CinematicAmbush>(FindObjectsInactive.Include))
            if (a.isBasementTransition && a.basementSpawnPoint != null) { drop = a.basementSpawnPoint; break; }
        if (drop == null && RespawnManager.instance != null) drop = RespawnManager.instance.basementRespawn;
        if (drop == null) { s.why = "no basement drop point"; return s; }

        Vector3 P = drop.position;
        if (NavMesh.SamplePosition(P, out NavMeshHit h, 3f, NavMesh.AllAreas)) P = h.position;
        s.cellCenter = P;

        // nearest door of this room that can be seen from the drop point
        DoorController best = null; float bestD = 9f; Vector3 bestC = Vector3.zero;
        DoorController any = null; float anyD = 9f; Vector3 anyC = Vector3.zero;
        foreach (DoorController d in FindObjectsByType<DoorController>(FindObjectsInactive.Include))
        {
            Collider dc = d.GetComponentInChildren<Collider>();
            Vector3 c = dc != null ? dc.bounds.center : d.transform.position;
            if (Mathf.Abs(c.y - P.y) > 2.6f) continue;
            float dist = Vector3.Distance(new Vector3(c.x, P.y, c.z), P);
            if (dist < anyD) { any = d; anyD = dist; anyC = c; }
            if (dist >= bestD) continue;
            Vector3 eye = P + Vector3.up * 1.2f, tgt = new Vector3(c.x, P.y + 1.2f, c.z);
            bool visible = true;
            foreach (RaycastHit rh in Physics.RaycastAll(eye, (tgt - eye).normalized, Vector3.Distance(eye, tgt) - 0.15f, ~0, QueryTriggerInteraction.Ignore))
                if (rh.collider.GetComponentInParent<DoorController>() != d && rh.collider.GetComponentInParent<PlayerController>() == null
                    && rh.collider.GetComponentInParent<EnemyAI>() == null) { visible = false; break; }
            if (!visible) continue;
            best = d; bestD = dist; bestC = c;
        }
        if (best == null && any != null) { best = any; bestC = anyC; }
        if (best == null) { s.why = "no visible door near the drop point " + P; return s; }

        s.door = best;
        Vector3 doorC = new Vector3(bestC.x, P.y, bestC.z);
        Vector3 inDir = P - doorC; inDir.y = 0f;
        if (inDir.sqrMagnitude < 0.04f) { s.why = "door on top of the drop point"; return s; }
        inDir.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, inDir).normalized;
        s.doorCenter = doorC;

        // camera: on the floor (sitting / lying against the wall), deeper in the cell, a bit to the side
        Vector3 cam = P + inDir * 0.6f + side * 0.35f;
        cam = Snap(cam, P.y);
        cam.y = P.y + 0.75f;
        s.camCell = cam;

        s.enemyDoor = Snap(doorC + inDir * 0.35f, P.y);
        Vector3 toDoor = s.enemyDoor - new Vector3(cam.x, P.y, cam.z); toDoor.y = 0f;
        s.enemyStand = Snap(new Vector3(cam.x, P.y, cam.z) + toDoor.normalized * Mathf.Min(1.15f, toDoor.magnitude * 0.6f), P.y);

        // corridor: the straightest walkable path away from the door
        Vector3 outside = Snap(doorC - inDir * 1.2f, P.y);
        Vector3[] dirs = { side, -side, -inDir, (side - inDir).normalized, (-side - inDir).normalized };
        float bestLen = 0f; Vector3 end = outside - inDir * 6f;
        foreach (Vector3 dir in dirs)
        {
            for (float dist = 13f; dist >= 5f; dist -= 2f)
            {
                if (!NavMesh.SamplePosition(outside + dir * dist, out NavMeshHit eh, 1.5f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(eh.position.y - outside.y) > 1f) continue;
                NavMeshPath p = new NavMeshPath();
                if (!NavMesh.CalculatePath(outside, eh.position, NavMesh.AllAreas, p) || p.status != NavMeshPathStatus.PathComplete) continue;
                float straight = Vector3.Distance(outside, eh.position), len = 0f;
                for (int i = 1; i < p.corners.Length; i++) len += Vector3.Distance(p.corners[i - 1], p.corners[i]);
                if (len > straight * 1.25f) continue;
                if (straight > bestLen) { bestLen = straight; end = eh.position; }
                break;
            }
        }
        s.walkEnd = end;
        Vector3 away = end - outside; away.y = 0f; away = away.sqrMagnitude > 0.01f ? away.normalized : -inDir;
        s.camCorridor = outside - away * 1.6f + Vector3.up * 1.55f;
        s.ok = true;
        return s;
    }

    private static Vector3 Snap(Vector3 p, float y)
    {
        if (NavMesh.SamplePosition(new Vector3(p.x, y, p.z), out NavMeshHit h, 1.2f, NavMesh.AllAreas)) return h.position;
        return new Vector3(p.x, y, p.z);
    }

    /// <summary>Uses hand-placed child markers when they exist (created from the menu).</summary>
    private Shots ShotsFromMarkers(Shots s)
    {
        Transform a = transform.Find("CamCell"), b = transform.Find("EnemyDoor"), c = transform.Find("EnemyStand"),
                  d = transform.Find("CamCorridor"), e = transform.Find("WalkEnd");
        if (a != null) s.camCell = a.position;
        if (b != null) s.enemyDoor = b.position;
        if (c != null) s.enemyStand = c.position;
        if (d != null) s.camCorridor = d.position;
        if (e != null) s.walkEnd = e.position;
        return s;
    }

    // ------------------------------------------------------------------ runtime state
    private Camera cam;
    private EnemyAI enemy;
    private Animator anim;
    private AudioSource voice;
    private float rootLift = 1f;
    private float black = 1f;
    private string subtitle = "", title = "", titleLine = "";
    private float titleAlpha, shakeAmp, startTime;
    private bool skipRequested;
    private Vector3 camVel;
    private Light keyLight, rimLight;
    private const string PunchState = "Hell Slammer A";   // the zombie swipe clip
    private float eyelid, bloodAlpha, redFlash, extraRoll;
    private AudioSource sfx;
    private AudioLowPassFilter muffle;

    private float HalfOpen() => 0.5f + Mathf.Sin(Time.time * 2.3f) * 0.07f + (Mathf.PerlinNoise(Time.time * 1.5f, 3f) - 0.5f) * 0.12f;

    /// <summary>Punch: a heavy thump + crack (made in code), then ears ringing and all sound muffled.</summary>
    private void PlayPunch()
    {
        if (sfx == null) return;
        const int sr = 44100;
        System.Random r = new System.Random(7);
        int n = (int)(sr * 0.6f);
        float[] d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            phase += 2f * Mathf.PI * (50f + 110f * Mathf.Exp(-t * 28f)) / sr;
            float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 8f);
            float crack = ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 45f);
            d[i] = Mathf.Clamp(thump * 1.1f + crack * 0.7f, -1f, 1f);
        }
        AudioClip punch = AudioClip.Create("Punch", n, 1, sr, false); punch.SetData(d, 0);
        int m = (int)(sr * 3.5f);
        float[] e = new float[m];
        for (int i = 0; i < m; i++)
        {
            float t = i / (float)sr;
            e[i] = Mathf.Sin(2f * Mathf.PI * 3600f * t) * 0.08f * Mathf.Clamp01(t * 6f) * Mathf.Clamp01((3.5f - t) / 2.5f);
        }
        AudioClip ring = AudioClip.Create("EarRing", m, 1, sr, false); ring.SetData(e, 0);
        sfx.PlayOneShot(punch, 1f);
        sfx.PlayOneShot(ring, 0.5f);
        if (muffle != null) muffle.cutoffFrequency = 900f;
    }



    public static IEnumerator Play()
    {
        ConsumedEndingCinematic c = FindAnyObjectByType<ConsumedEndingCinematic>();
        if (c == null) c = new GameObject("ConsumedEndingCinematic").AddComponent<ConsumedEndingCinematic>();
        yield return c.StartCoroutine(c.Run());
    }

    public static void PlayNow()
    {
        if (IsPlaying) return;
        ConsumedEndingCinematic c = FindAnyObjectByType<ConsumedEndingCinematic>();
        if (c == null) c = new GameObject("ConsumedEndingCinematic").AddComponent<ConsumedEndingCinematic>();
        c.StartCoroutine(c.Run());
    }

    private void Update()
    {
        if (!IsPlaying) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (Time.time - startTime > 2f && Input.anyKeyDown) skipRequested = true;
    }

    // ------------------------------------------------------------------ the cinematic
    private IEnumerator Run()
    {
        IsPlaying = true;
        startTime = Time.time;
        Time.timeScale = 1f;

        Shots s = ShotsFromMarkers(Compute());
        if (!s.ok || !SetupActors(s))
        {
            Debug.LogWarning("[Ending] Consumed cinematic could not be placed (" + s.why + ") - showing the title card.");
            yield return TitleCard();
            yield break;
        }
        CinematicAmbush.AmbushLog("[Ending] cam=" + s.camCell + " door=" + s.enemyDoor + " stand=" + s.enemyStand + " corridor=" + s.camCorridor + " end=" + s.walkEnd);

        List<Light> roomLights = LightsNear(s.cellCenter, 5f, null);
        float floorY = s.enemyDoor.y;

        // ---- 1. black
        black = 1f;
        subtitle = openingLine;
        SetCam(s.camCell, s.enemyDoor + Vector3.up * 1.5f, 55f);
        yield return Wait(3f);
        subtitle = "";
        if (skipRequested) { yield return TitleCard(); yield break; }

        // ---- 2. eyes open: he is in the doorway
        PlaceEnemy(s.enemyDoor, s.camCell - s.enemyDoor);
        Pose("Male Standing Pose", 0f);
        float t = 0f;
        while (t < 6f && !skipRequested)
        {
            t += Time.deltaTime;
            black = t < 0.5f ? 1f : t < 1.3f ? Mathf.Lerp(1f, 0.3f, (t - 0.5f) / 0.8f) : t < 1.6f ? 0.85f : Mathf.Max(0f, 0.85f - (t - 1.6f) * 0.8f);
            Vector3 breathe = new Vector3(0f, Mathf.Sin(t * 1.1f) * 0.012f, 0f);
            Frame(s.camCell + breathe, Head(s.enemyDoor), 55f, 2.5f);
            Flicker(roomLights, 0.4f);
            yield return null;
        }
        if (skipRequested) { yield return TitleCard(); yield break; }

        // ---- 3. he walks in and stands over you
        Pose("Walking", 1f);
        yield return WalkTo(s.enemyDoor, s.enemyStand, 0.85f, pos =>
        {
            Frame(s.camCell, Head(pos), 55f, 3f);
            Flicker(roomLights, 0.25f);
        });
        if (skipRequested) { yield return TitleCard(); yield break; }

        // ---- 4. his line
        Pose("Male Standing Pose", 0f);
        FaceEnemy(s.camCell - s.enemyStand);
        if (anim != null) anim.SetTrigger("Tease");
        float lineLen = PlayVoice();
        subtitle = spokenLine;
        t = 0f;
        Vector3 pushIn = s.camCell + (new Vector3(s.enemyStand.x, s.camCell.y, s.enemyStand.z) - s.camCell) * 0.15f;
        while (t < lineLen + 0.6f && !skipRequested)
        {
            t += Time.deltaTime;
            shakeAmp = t > 0.4f ? Mathf.Lerp(0.9f, 0.25f, (t - 0.4f) / lineLen) : 0f;
            Vector3 face = FaceOfEnemy(s.enemyStand);
            Frame(Vector3.Lerp(s.camCell, pushIn, Mathf.SmoothStep(0f, 1f, t / lineLen)), face, Mathf.Lerp(55f, 44f, t / lineLen), 4f);
            Flicker(roomLights, 0.5f);
            yield return null;
        }
        subtitle = "";
        shakeAmp = 0f;
        if (voice != null) voice.Stop();
        if (skipRequested) { yield return TitleCard(); yield break; }

        // ---- 5. he turns to leave... then spins back and punches you
        Vector3 outside = Snap(s.doorCenter - (s.cellCenter - s.doorCenter).normalized * 1.0f, floorY);
        Quaternion faceCam = enemy.transform.rotation;
        Vector3 toDoor = s.enemyDoor - s.enemyStand; toDoor.y = 0f;
        Quaternion faceDoor = toDoor.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toDoor.normalized) : faceCam;
        t = 0f;
        while (t < 1.3f && !skipRequested)                      // he turns away... a pause...
        {
            t += Time.deltaTime;
            enemy.transform.rotation = Quaternion.Slerp(faceCam, faceDoor, Mathf.SmoothStep(0f, 1f, t / 0.9f));
            enemy.KeepGrounded();
            Frame(pushIn, FaceOfEnemy(s.enemyStand), 48f, 3f);
            Flicker(roomLights, 0.3f);
            yield return null;
        }
        if (skipRequested) { yield return TitleCard(); yield break; }
        if (anim != null)                                        // ...whips back round and swings
        {
            anim.speed = 1.25f;
            if (anim.HasState(0, Animator.StringToHash(PunchState))) anim.CrossFadeInFixedTime(PunchState, 0.08f);
            else anim.SetTrigger("Attack");
        }
        float hitAt = 1.0f / 1.25f;                              // the swipe lands ~1 s into the clip
        t = 0f;
        while (t < hitAt && !skipRequested)
        {
            t += Time.deltaTime;
            enemy.transform.rotation = Quaternion.Slerp(faceDoor, faceCam, Mathf.Clamp01(t / 0.25f));
            enemy.KeepGrounded();
            Frame(pushIn, FaceOfEnemy(s.enemyStand), Mathf.Lerp(48f, 40f, t / hitAt), 6f);
            yield return null;
        }

        // HIT: crack, red flash, blood, the camera falls to the floor, eyes half shut, ears ringing
        PlayPunch();
        redFlash = 1f; bloodAlpha = 1f; shakeAmp = 2.4f;
        Vector3 flatIn = new Vector3(s.enemyStand.x - pushIn.x, 0f, s.enemyStand.z - pushIn.z).normalized;
        Vector3 fallPos = new Vector3(pushIn.x, floorY + 0.25f, pushIn.z) - flatIn * 0.2f + Vector3.Cross(Vector3.up, flatIn) * 0.15f;
        t = 0f;
        while (t < 2.4f)
        {
            t += Time.deltaTime;
            redFlash = Mathf.Max(0f, 1f - t * 2.5f);
            shakeAmp = Mathf.Max(0f, 2.4f - t * 3f);
            float fall = Mathf.SmoothStep(0f, 1f, t / 0.7f);
            extraRoll = Mathf.Lerp(0f, 58f, fall);
            eyelid = t < 0.12f ? Mathf.Lerp(0f, 0.9f, t / 0.12f) : t < 0.9f ? 0.9f : Mathf.Lerp(0.9f, 0.5f, (t - 0.9f) / 1.2f);
            Frame(Vector3.Lerp(pushIn, fallPos, fall), Head(s.enemyStand), 52f, 8f, 0.06f);
            yield return null;
        }
        if (anim != null) anim.speed = 1f;

        // through half-open eyes: he walks out, the door slams, the lamp dies
        Pose("Walking", 1f);
        yield return WalkTo(s.enemyStand, s.enemyDoor, 1.0f, pos => { eyelid = HalfOpen(); Frame(fallPos, Head(pos), 52f, 2f, 0.3f); });
        yield return WalkTo(s.enemyDoor, outside, 1.0f, pos => { eyelid = HalfOpen(); Frame(fallPos, Head(pos), 52f, 2f, 0.3f); });
        if (s.door != null) s.door.TrapLock();
        foreach (Light l in roomLights) if (l != null) l.enabled = false;
        if (keyLight != null) keyLight.intensity = 0.25f;
        shakeAmp = 0.8f;
        t = 0f;
        while (t < 2.2f)                                         // eyes close (one last flutter)
        {
            t += Time.deltaTime;
            shakeAmp = Mathf.Max(0f, 0.8f - t * 1.5f);
            eyelid = t < 0.8f ? Mathf.Lerp(0.55f, 0.92f, t / 0.8f) : t < 1.1f ? Mathf.Lerp(0.92f, 0.75f, (t - 0.8f) / 0.3f) : Mathf.Lerp(0.75f, 1f, (t - 1.1f) / 0.9f);
            Frame(fallPos, s.doorCenter + Vector3.up * 1.2f, 52f, 2f, 0.3f);
            yield return null;
        }
        black = 1f; eyelid = 0f; bloodAlpha = 0f; extraRoll = 0f; shakeAmp = 0f;
        if (muffle != null) muffle.cutoffFrequency = 22000f;
        yield return Wait(1.2f);

        // ---- 6. corridor: he walks away, the lights die towards the camera
        if (keyLight != null) keyLight.enabled = false;
        if (s.door != null) s.door.OpenForCinematic();       // only for the shot - he is outside anyway
        List<Vector3> route = Route(outside, s.walkEnd);
        float total = 0f; for (int i = 1; i < route.Count; i++) total += Vector3.Distance(route[i - 1], route[i]);
        List<Light> hall = LightsNear((outside + s.walkEnd) * 0.5f, Mathf.Max(8f, total * 0.7f), s.cellCenter);
        hall.Sort((a, b) => Vector3.Distance(b.transform.position, s.camCorridor).CompareTo(Vector3.Distance(a.transform.position, s.camCorridor)));
        if (s.door != null) s.door.TrapLock();
        PlaceEnemy(outside, s.walkEnd - outside);
        Pose("Walking", 1f);
        SetCam(s.camCorridor, Head(outside), 50f);
        float dur = Mathf.Clamp(total / 1.1f, 4f, 9f);
        if (anim != null) anim.speed = Mathf.Clamp((total / dur) / 1.07f, 0.7f, 1.4f);
        t = 0f; int off = 0;
        while (t < dur && !skipRequested)
        {
            t += Time.deltaTime;
            black = Mathf.Max(0f, 1f - t * 1.5f);
            Vector3 pos = AlongRoute(route, total * (t / dur), out Vector3 dir);
            MoveEnemy(pos, dir);
            Frame(s.camCorridor, Head(pos), Mathf.Lerp(50f, 40f, t / dur), 2f);
            int want = Mathf.FloorToInt(hall.Count * Mathf.Clamp01((t - 1.5f) / (dur - 1.5f)));
            while (off < want) { if (hall[off] != null) hall[off].enabled = false; off++; }
            yield return null;
        }
        if (anim != null) anim.speed = 1f;
        t = 0f;
        while (t < 1.2f) { t += Time.deltaTime; black = t / 1.2f; yield return null; }

        // ---- 7. title card
        yield return TitleCard();
    }

    private IEnumerator TitleCard()
    {
        subtitle = "";
        black = 1f;
        if (voice != null) voice.Stop();
        title = endTitle; titleLine = endLine;
        float t = 0f;
        while (t < 5.5f)
        {
            t += Time.deltaTime;
            titleAlpha = t < 1.5f ? t / 1.5f : t > 4.5f ? Mathf.Max(0f, 5.5f - t) : 1f;
            yield return null;
        }
        IsPlaying = false;
        Time.timeScale = 1f;
        IntroState.skipIntro = false;
        SceneLoader.Load(menuScene);
    }

    // ------------------------------------------------------------------ setup
    private bool SetupActors(Shots s)
    {
        enemy = FindAnyObjectByType<EnemyAI>(FindObjectsInactive.Include);
        if (enemy == null) { CinematicAmbush.AmbushLog("[Ending] no enemy"); return false; }
        enemy.EnsureActive();
        foreach (MonoBehaviour mb in enemy.GetComponentsInChildren<MonoBehaviour>())
            if (mb is EnemyHorrorFX || mb is EnemyFootIK) mb.enabled = false;
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null) { rootLift = agent.baseOffset; agent.enabled = false; }
        enemy.enabled = false;
        anim = enemy.enemyAnimator;
        if (anim != null)
        {
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.speed = 1f;
            anim.ResetTrigger("Attack"); anim.ResetTrigger("Tease");
        }
        foreach (AudioSource a in enemy.GetComponentsInChildren<AudioSource>()) a.Stop();

        if (s.door != null) s.door.OpenForCinematic();

        // the player: hidden, frozen, headlamp off
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        Camera src = pc != null ? pc.GetComponentInChildren<Camera>(true) : Camera.main;
        if (pc != null)
        {
            pc.SetBodyVisible(false);
            pc.enabled = false;
            foreach (Light l in pc.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (InteractionRaycast r in pc.GetComponentsInChildren<InteractionRaycast>(true)) r.enabled = false;
            foreach (AudioSource a in pc.GetComponentsInChildren<AudioSource>()) a.Stop();
            CharacterController cc = pc.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            pc.transform.position = s.cellCenter + Vector3.down * 30f;   // out of every shot
        }

        // its own camera, every other camera and every UI canvas off
        GameObject camGo = new GameObject("CinematicCamera");
        cam = camGo.AddComponent<Camera>();
        if (src != null) cam.CopyFrom(src);
        cam.targetTexture = null;
        cam.rect = new Rect(0, 0, 1, 1);
        cam.depth = 100;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 120f;
        int ui = LayerMask.NameToLayer("UI");
        cam.cullingMask = ui >= 0 ? ~(1 << ui) : ~0;
        cam.fieldOfView = 55f;
        cam.enabled = true;
        camGo.AddComponent<AudioListener>();
        muffle = camGo.AddComponent<AudioLowPassFilter>();
        muffle.cutoffFrequency = 22000f;
        sfx = camGo.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 0f;
        overlay = TLCCanvas.Ensure();
        foreach (Camera c in FindObjectsByType<Camera>(FindObjectsInactive.Exclude)) if (c != cam) c.enabled = false;
        foreach (AudioListener al in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude)) if (al.gameObject != camGo) al.enabled = false;
        foreach (Canvas cv in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)) if (cv.isRootCanvas && cv.GetComponent<TLCCanvas>() == null) cv.enabled = false;

        // soft cold key light on the camera + rim light over the clown (no shadows = no lag)
        keyLight = MakeLight("CinematicKeyLight", camGo.transform, new Color(0.7f, 0.76f, 0.9f), 1.3f, 6f);
        keyLight.transform.localPosition = new Vector3(0.3f, 0.4f, 0.1f);
        rimLight = MakeLight("CinematicRimLight", enemy.transform, new Color(0.85f, 0.88f, 1f), 2f, 4f);
        rimLight.transform.localPosition = new Vector3(0f, 2.4f, -0.7f);

        // his voice comes from his mouth
        if (voiceClip == null) voiceClip = Resources.Load<AudioClip>("HorrorAudio/Joker_You_Stay_Here");
        voice = enemy.gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0.6f;
        voice.minDistance = 2f;
        voice.volume = voiceVolume;
        return true;
    }

    private float PlayVoice()
    {
        if (voice != null && voiceClip != null) { voice.clip = voiceClip; voice.Play(); return Mathf.Max(3f, voiceClip.length); }
        if (enemy != null) enemy.PlayTeaseLine();
        return 4.2f;
    }

    private static Light MakeLight(string n, Transform parent, Color c, float intensity, float range)
    {
        GameObject g = new GameObject(n);
        g.transform.SetParent(parent, false);
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        return l;
    }

    // ------------------------------------------------------------------ camera
    private void SetCam(Vector3 pos, Vector3 target, float fov)
    {
        if (cam == null) return;
        cam.transform.position = pos;
        cam.transform.rotation = ClampPitch(Quaternion.LookRotation(target - pos));
        cam.fieldOfView = fov;
        camVel = Vector3.zero;
    }

    /// <summary>Smooth camera: damped position, eased rotation, pitch limited so it never stares at the ceiling.</summary>
    private void Frame(Vector3 pos, Vector3 target, float fov, float rotSharpness, float smoothTime = 0.35f)
    {
        if (cam == null) return;
        float dt = Time.deltaTime, tt = Time.time;
        Vector3 p = Vector3.SmoothDamp(cam.transform.position - ShakeOffset(lastShakeTime), pos, ref camVel, smoothTime, 6f, dt);
        lastShakeTime = tt;
        cam.transform.position = p + ShakeOffset(tt);
        Quaternion want = ClampPitch(Quaternion.LookRotation((target - p).sqrMagnitude > 0.001f ? target - p : cam.transform.forward));
        float roll = shakeAmp > 0f ? (Mathf.PerlinNoise(tt * 14f, 9f) - 0.5f) * 5f * shakeAmp : 0f;
        want *= Quaternion.Euler(0f, 0f, roll + extraRoll);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, want, 1f - Mathf.Exp(-rotSharpness * dt));
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-2f * dt));
    }

    private float lastShakeTime;
    private Vector3 ShakeOffset(float tt)
    {
        if (shakeAmp <= 0f) return Vector3.zero;
        return new Vector3(Mathf.PerlinNoise(tt * 18f, 1f) - 0.5f, Mathf.PerlinNoise(tt * 19f, 5f) - 0.5f, 0f) * 0.05f * shakeAmp;
    }

    private static Quaternion ClampPitch(Quaternion q)
    {
        Vector3 e = q.eulerAngles;
        float pitch = e.x > 180f ? e.x - 360f : e.x;      // negative = looking up
        pitch = Mathf.Clamp(pitch, -38f, 20f);
        return Quaternion.Euler(pitch, e.y, 0f);
    }

    // ------------------------------------------------------------------ enemy
    private static Vector3 Head(Vector3 floorPos) => floorPos + Vector3.up * 1.55f;

    private Vector3 FaceOfEnemy(Vector3 floorPos)
    {
        Transform hb = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        Vector3 h = hb != null ? hb.position : Head(floorPos);
        smoothFace = smoothFace == Vector3.zero ? h : Vector3.Lerp(smoothFace, h, 1f - Mathf.Exp(-6f * Time.deltaTime));
        return smoothFace;
    }
    private Vector3 smoothFace;

    private void PlaceEnemy(Vector3 floorPos, Vector3 facing)
    {
        if (enemy == null) return;
        enemy.transform.position = floorPos + Vector3.up * rootLift;
        FaceEnemy(facing);
        enemy.KeepGrounded();
    }

    private void MoveEnemy(Vector3 floorPos, Vector3 facing)
    {
        if (enemy == null) return;
        enemy.transform.position = floorPos + Vector3.up * rootLift;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f)
            enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, Quaternion.LookRotation(facing.normalized), 1f - Mathf.Exp(-6f * Time.deltaTime));
        enemy.KeepGrounded();
    }

    private void FaceEnemy(Vector3 facing)
    {
        facing.y = 0f;
        if (enemy != null && facing.sqrMagnitude > 0.001f) enemy.transform.rotation = Quaternion.LookRotation(facing.normalized);
    }

    private void Pose(string state, float speed)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;
        anim.SetFloat("Speed", speed);
        anim.CrossFadeInFixedTime(state, 0.3f);
    }

    private IEnumerator WalkTo(Vector3 from, Vector3 to, float speed, System.Action<Vector3> each)
    {
        float dur = Mathf.Max(0.4f, Vector3.Distance(from, to) / speed);
        if (anim != null) anim.speed = Mathf.Clamp(speed / 1.07f, 0.7f, 1.4f);
        float t = 0f;
        while (t < dur && !skipRequested)
        {
            t += Time.deltaTime;
            Vector3 pos = Vector3.Lerp(from, to, Mathf.Clamp01(t / dur));
            MoveEnemy(pos, to - from);
            each?.Invoke(pos);
            yield return null;
        }
        if (anim != null) anim.speed = 1f;
    }

    private IEnumerator Wait(float sec) { float t = 0f; while (t < sec && !skipRequested) { t += Time.deltaTime; yield return null; } }

    // ------------------------------------------------------------------ lights
    private static List<Light> LightsNear(Vector3 c, float radius, Vector3? exclude)
    {
        List<Light> list = new List<Light>();
        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            if (!l.enabled || l.type == LightType.Directional) continue;
            Vector3 p = l.transform.position;
            if (Mathf.Abs(p.y - c.y) > 5f) continue;
            if (new Vector2(p.x - c.x, p.z - c.z).magnitude > radius) continue;
            if (exclude.HasValue && new Vector2(p.x - exclude.Value.x, p.z - exclude.Value.z).magnitude < 3f) continue;
            if (l.GetComponentInParent<PlayerController>() != null || l.name.StartsWith("Cinematic")) continue;
            list.Add(l);
        }
        return list;
    }

    // stepped flicker (changes ~14 times a second, not every frame - smoother and cheaper)
    private readonly Dictionary<Light, float> baseIntensity = new Dictionary<Light, float>();
    private float nextFlicker;
    private void Flicker(List<Light> lights, float strength)
    {
        if (Time.time < nextFlicker) return;
        nextFlicker = Time.time + 0.07f;
        foreach (Light l in lights)
        {
            if (l == null || !l.enabled) continue;
            if (!baseIntensity.ContainsKey(l)) baseIntensity[l] = l.intensity;
            l.intensity = baseIntensity[l] * (Random.value < strength * 0.45f ? 0.12f : 1f);
        }
    }

    // ------------------------------------------------------------------ route
    private static List<Vector3> Route(Vector3 a, Vector3 b)
    {
        List<Vector3> r = new List<Vector3>();
        NavMeshPath p = new NavMeshPath();
        if (NavMesh.CalculatePath(a, b, NavMesh.AllAreas, p) && p.status == NavMeshPathStatus.PathComplete && p.corners.Length > 1) r.AddRange(p.corners);
        else { r.Add(a); r.Add(b); }
        return r;
    }

    private static Vector3 AlongRoute(List<Vector3> r, float d, out Vector3 dir)
    {
        dir = r[1] - r[0];
        for (int i = 1; i < r.Count; i++)
        {
            float seg = Vector3.Distance(r[i - 1], r[i]);
            if (d <= seg || i == r.Count - 1)
            {
                dir = r[i] - r[i - 1];
                return Vector3.Lerp(r[i - 1], r[i], seg > 0.001f ? Mathf.Clamp01(d / seg) : 1f);
            }
            d -= seg;
        }
        return r[r.Count - 1];
    }

    // ------------------------------------------------------------------ overlay
    // ------------------------------------------------------------------ overlay: real objects on the TLC_UI canvas (CinematicOverlay)
    private TLCCanvas overlay;

    private void LateUpdate()
    {
        if (!IsPlaying) return;
        if (overlay == null) overlay = TLCCanvas.Ensure();
        if (overlay == null || overlay.cinematic == null) return;
        TLCCanvas o = overlay;
        TLCCanvas.SetActive(o.cinematic, true);
        TLCCanvas.SetActive(o.hud, false);
        TLCCanvas.SetActive(o.objectivePanel, false);
        o.cinematic.transform.SetAsLastSibling();

        o.cineBlack.color = new Color(0f, 0f, 0f, black);
        o.cineBlood.color = new Color(1f, 1f, 1f, bloodAlpha);
        o.cineRed.color = new Color(0.55f, 0f, 0f, redFlash * 0.75f);
        float lidH = eyelid > 0f ? eyelid * 540f + 150f : 0f;      // canvas space is 1080 high
        o.lidTop.rectTransform.sizeDelta = new Vector2(0f, lidH);
        o.lidBottom.rectTransform.sizeDelta = new Vector2(0f, lidH);

        TLCCanvas.SetText(o.cineSubtitle, subtitle);
        TLCCanvas.SetText(o.cineTitle, title);
        TLCCanvas.SetText(o.cineTitleLine, string.IsNullOrEmpty(title) ? "" : titleLine);
        if (!string.IsNullOrEmpty(title))
        {
            o.cineTitle.color = new Color(0.72f, 0.18f, 0.1f, titleAlpha);
            o.cineTitleLine.color = new Color(0.85f, 0.8f, 0.72f, titleAlpha);
        }
        TLCCanvas.SetActive(o.cineSkip.gameObject, Time.time - startTime > 2f && string.IsNullOrEmpty(title));
    }



#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        string[] names = { "CamCell", "EnemyDoor", "EnemyStand", "CamCorridor", "WalkEnd" };
        Color[] cols = { Color.cyan, Color.red, new Color(1f, 0.5f, 0f), Color.green, Color.magenta };
        for (int i = 0; i < names.Length; i++)
        {
            Transform m = transform.Find(names[i]);
            if (m == null) continue;
            Gizmos.color = cols[i];
            Gizmos.DrawWireSphere(m.position, 0.2f);
            if (i == 1 || i == 2 || i == 4) Gizmos.DrawLine(m.position, m.position + Vector3.up * 1.8f);
            UnityEditor.Handles.Label(m.position + Vector3.up * 0.3f, names[i]);
        }
    }
#endif
}
