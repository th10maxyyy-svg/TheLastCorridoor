using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// THE GRAND ESCAPE - first-person horror ending.
///
///  1. The player reaches the gate. A blink... and he has turned around, back towards the dark hall.
///  2. Deep in the hall a dying red bulb flickers on: Brother Silas is standing there, staring. He screams.
///  3. He CHARGES. Camera locked on his face, violent shake, heartbeat, the player stumbles back.
///  4. Inches away, sunlight explodes through the gate behind the player - white-out, slow motion.
///     He burns, shrieks and flees back into the darkness while the hall lights die one by one behind him.
///  5. The player turns towards the light, walks out... white -> black -> THE END.
///
/// Uses the points placed in the scene: enemyTeleportPoint (where he appears), enemyDoorPoint (where the sun stops him),
/// enemyRetreatPoint (where he flees to). Everything else is computed from the gate (this object).
/// </summary>
public class EscapeCinematicManager : MonoBehaviour
{
    [Header("Cinematic Elements")]
    [Tooltip("(legacy - the ending is now shown through the player's own eyes)")]
    public Camera cinematicCamera;
    public CanvasGroup fadeCanvasGroup;
    public TextMeshProUGUI endingText;

    [Header("Camera Movement & Shake")]
    public Transform cameraStartPoint;   // (legacy - not needed any more)
    public Transform cameraEndPoint;     // (legacy - not needed any more)
    public float cameraMoveSpeed = 1f;   // (legacy)
    public float cameraShakeAmount = 0.05f;
    public float cameraShakeSpeed = 20f;

    [Header("Enemy Control")]
    public EnemyAI enemyAI;
    [Tooltip("Where he appears, deep inside the hall")]
    public Transform enemyTeleportPoint;
    [Tooltip("Where the sunlight stops him (close to the gate, inside the building)")]
    public Transform enemyDoorPoint;
    [Tooltip("Where he runs back to in the darkness")]
    public Transform enemyRetreatPoint;
    public string enemyAnimationTriggerName = "Tease";
    [Tooltip("Animator states (clownanimator.controller) used by the escape - made by The Last Corridor / Setup Joker Escape Animations. Missing states fall back to the old trigger.")]
    public string enemyIdleState = "Mutant Idle";
    public string enemyRoarState = "Mutant Roar";
    public string enemyRunState = "Mutant Run";
    public string enemyLightHitState = "Light Hit";
    public string enemyAgonyState = "Agony";

    [Header("Settings")]
    [Tooltip("Small line shown under THE END")]
    public string finalMessage = "The Nightmare is over... for now.";
    public float fadeDuration = 2.5f;
    public float timeBeforeFade = 6f; // (legacy)

    [Header("Optional Extra Sounds")]
    [Tooltip("Sharp horror sting when he appears")]
    public AudioClip appearSting;
    [Tooltip("Played when the sunlight bursts in")]
    public AudioClip sunBurstSound;
    [Tooltip("Heartbeat loop during the chase")]
    public AudioClip heartbeatLoop;

    // ---- tuning (constants on purpose: old scene-serialized values must never override them) ----
    // CAMERA / WORLD FIX: nothing is built outside the asylum (empty world), and the main door may still be closed when the
    // ending starts. So the player now stands INSIDE, just behind the door, and the camera only ever looks into the hall.
    // The door is thrown open by the sunlight at the burst; the empty outside is only ever seen through the white-out.
    private const float playerInsideDepth = 0.9f;  // player stands this far INSIDE the main door
    private const float enemyStopGap = 2.9f;       // the sun stops him this far in front of the player
    private const float enemyMinGap = 2.4f;        // he can NEVER get closer to the player than this
    private const float fallbackSpawnDepth = 9f;   // if no teleport point: metres inside the hall
    private const float fallbackStopDepth = 2.2f;  // if no door point: where the sun stops him
    private const float stareTime = 3.4f;          // how long he just stands and stares before charging
    private const float maxChargeTime = 6f;        // safety timeout for the charge
    private const float screamTime = 1.8f;         // burning/screaming at the light
    private const float fleeWatchTime = 2.6f;      // watching him vanish into the dark
    private const float sunBurst = 14f;     // warm light through the closed gate (was 60 = everything washed out)
    private static readonly Color sunColor = new Color(1f, 0.95f, 0.82f);
    private static readonly Color bulbColor = new Color(1f, 0.28f, 0.18f);

    private bool hasTriggered = false;
    private bool running = false;

    // geometry
    private Vector3 gatePos, inward, outward;
    private Transform stopPoint;
    private DoorController mainDoor;   // the exit door next to the gate (opened by the sunlight)
    private float playerDepth;         // metres inside the gate line where the player stands

    // camera tracking (a steady point on his body, not the bouncing head bone)
    private float headHeight = 1.6f;
    private Vector3 trackPos, trackVel;
    private bool trackInit = false;

    // player
    private Transform player;
    private Camera playerCam;
    private float baseFov = 60f;
    private PlayerController playerController;
    private PlayerStats playerStats;

    // camera state
    private enum CamMode { None, LookAtEnemy, Free }
    private CamMode camMode = CamMode.None;
    private float shake = 0f;          // 0..1 handheld shake strength
    private float fovTarget;
    private float lookSharpness = 3f;  // how fast the eyes follow him

    // fx
    private Light sun;
    private Light bulb;
    private float bulbBase = 0f;
    private CanvasGroup flash;
    private AudioSource sfx;
    private AudioSource heartbeat;
    private readonly List<Light> hallLights = new List<Light>();

    // ------------------------------------------------------------------ setup
    private void Start()
    {
        if (cinematicCamera != null) cinematicCamera.gameObject.SetActive(false);
        if (endingText != null) endingText.text = "";

        ComputeGeometry();
        ExtendTriggerBackwards();
    }

    private void ComputeGeometry()
    {
        gatePos = transform.position;

        Vector3 inDir = Vector3.zero;
        if (enemyTeleportPoint != null) inDir = enemyTeleportPoint.position - gatePos;
        else if (enemyDoorPoint != null) inDir = enemyDoorPoint.position - gatePos;
        else if (enemyAI != null) inDir = enemyAI.transform.position - gatePos;
        inDir.y = 0f;
        if (inDir.sqrMagnitude < 0.01f) inDir = transform.forward;

        inward = inDir.normalized;
        outward = -inward;

        // the exit door sits right at the gate: find it so the player can be placed behind it
        float best = 3.5f;
        foreach (DoorController d in FindObjectsByType<DoorController>())
        {
            float dist = Vector3.Distance(Flat(d.transform.position), Flat(gatePos));
            if (dist < best) { best = dist; mainDoor = d; }
        }
        float doorDepth = mainDoor != null ? Mathf.Max(0f, Vector3.Dot(mainDoor.transform.position - gatePos, inward)) : 0f;
        playerDepth = doorDepth + playerInsideDepth;
    }

    /// <summary>Add a trigger volume in front of the gate (inside the building) so the cinematic starts a little early.</summary>
    private void ExtendTriggerBackwards()
    {
        const float depth = 3f, width = 3.4f, height = 3f;
        BoxCollider bc = gameObject.AddComponent<BoxCollider>();
        bc.isTrigger = true;

        Vector3 localIn = transform.InverseTransformDirection(inward);
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Approximately(s.x, 0f) ? 1f : Mathf.Abs(s.x);
        float sy = Mathf.Approximately(s.y, 0f) ? 1f : Mathf.Abs(s.y);
        float sz = Mathf.Approximately(s.z, 0f) ? 1f : Mathf.Abs(s.z);

        if (Mathf.Abs(localIn.z) >= Mathf.Abs(localIn.x))
        {
            float dir = Mathf.Sign(localIn.z);
            bc.size = new Vector3(width / sx, height / sy, depth / sz);
            bc.center = new Vector3(0f, 1f / sy, dir * (depth * 0.5f) / sz);
        }
        else
        {
            float dir = Mathf.Sign(localIn.x);
            bc.size = new Vector3(depth / sx, height / sy, width / sz);
            bc.center = new Vector3(dir * (depth * 0.5f) / sx, 1f / sy, 0f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!MainGateLock.AllowEscape) return;          // the main gate still needs its 5 keys
        if (!(other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)) return;

        PlayerStats ps = other.GetComponent<PlayerStats>();
        if (ps != null && ps.IsDead()) return;

        hasTriggered = true;
        StartCoroutine(PlayCinematicSequence(other.gameObject));
    }

    // ------------------------------------------------------------------ test button (menu: The Last Corridor / Play Escape Cinematic)
    /// <summary>True while the cinematic runs from the editor test button: nothing is saved, the save file is kept,
    /// and Play mode stops after THE END (no summary, no main menu).</summary>
    public static bool TestRun { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTestRun() { TestRun = false; }

    /// <summary>Plays the Grand Escape right now (editor test button).</summary>
    public static void PlayNow()
    {
        EscapeCinematicManager m = FindAnyObjectByType<EscapeCinematicManager>(FindObjectsInactive.Include);
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (m == null || pc == null) { Debug.LogWarning("[TLC] Escape cinematic test: no 'escape triger' or no Player in this scene."); return; }
        if (m.hasTriggered) { Debug.LogWarning("[TLC] The escape cinematic is already playing."); return; }
        TestRun = true;
        m.gameObject.SetActive(true);
        Debug.Log("[TLC] TEST: Grand Escape cinematic (nothing is saved, Play mode stops at the end).");
        m.BeginEscape(pc.gameObject, 0f);
    }

    /// <summary>Started by the MainGateLock when the last key goes in (the player is already standing at the gate).</summary>
    public void BeginEscape(GameObject playerObj, float delay)
    {
        if (hasTriggered || playerObj == null) return;
        hasTriggered = true;
        StartCoroutine(BeginAfter(playerObj, delay));
    }

    private IEnumerator BeginAfter(GameObject playerObj, float delay)
    {
        yield return new WaitForSeconds(delay);
        yield return PlayCinematicSequence(playerObj);
    }

    // ------------------------------------------------------------------ main sequence
    private IEnumerator PlayCinematicSequence(GameObject playerObj)
    {
        Debug.Log("Starting Grand Escape Cinematic!");
        player = playerObj.transform;
        playerController = playerObj.GetComponent<PlayerController>();
        playerStats = playerObj.GetComponent<PlayerStats>();
        playerCam = playerObj.GetComponentInChildren<Camera>();
        if (playerCam == null) playerCam = Camera.main;

        // ---- 1. lock everything down ----
        if (playerController != null)
        {
            playerController.isCinematicPlaying = true;
            playerController.enabled = false;
        }
        if (playerStats != null)
        {
            playerStats.isInvulnerable = true;
            playerStats.muteBreathing = true;   // no player breathing in this ending - only the joker is heard
        }
        if (playerCam != null)
        {
            InteractionRaycast ir = playerCam.GetComponent<InteractionRaycast>();
            if (ir != null) ir.enabled = false;
            baseFov = playerCam.fieldOfView;
            fovTarget = baseFov;
        }

        LoadDefaultSounds();
        HideGameplayHud();
        CreateFx();
        StartCoroutine(DuckWorldAudio(0.8f)); // only the ending's own sounds may be heard

        ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
        if (pm != null) pm.StartEscapePhase();

        // ---- 2. blink: when the eyes open he has turned round, back towards the dark hall ----
        yield return FadeFlash(Color.black, 0f, 1f, 0.25f);
        PlacePlayerAtGate();
        PrepareEnemy();
        CollectHallLights();
        running = true;
        camMode = CamMode.LookAtEnemy;
        lookSharpness = 3f;
        CaptureCameraBase();
        breath = 1f; bob = 0f; rollTarget = 0f; offsetTarget = Vector3.zero;
        SnapCameraToEnemy();
        yield return new WaitForSeconds(0.35f);
        yield return FadeFlash(Color.black, 1f, 0f, 0.9f);

        // ---- 3. the red bulb stutters on - he is standing deep in the hall, staring ----
        StartCoroutine(BulbFlicker());
        PlayCue(appearSting, 1f, 2.8f, 1.4f);
        fovTarget = baseFov * 0.8f;          // slow push towards him
        shake = 0.12f;
        offsetTarget = new Vector3(0f, 0f, 0.08f);   // the player leans in a little, trying to see what is there
        yield return new WaitForSeconds(1.3f);

        if (enemyAI != null)
        {
            StartCoroutine(PlayCueAfter(0.35f, voiceLine, 0.95f, voiceLine != null ? Mathf.Min(voiceLine.length, 4.5f) : 3.4f, 0.9f));
            if (PlayEnemyState(enemyRoarState, 0.25f))
                StartCoroutine(EnemyStateThen(enemyRoarState, enemyIdleState, 0.3f));   // roar, then back to breathing
            else if (!string.IsNullOrEmpty(enemyAnimationTriggerName) && enemyAI.enemyAnimator != null)
                enemyAI.enemyAnimator.SetTrigger(enemyAnimationTriggerName);
        }
        // the ROAR hits the player like a wave: he flinches back, the view tilts
        StartCoroutine(After(0.45f, () =>
        {
            Kick(new Vector3(-7f, 0f, 5f), 0.14f);
            fovTarget = baseFov * 0.9f;
            offsetTarget = new Vector3(0f, -0.04f, -0.12f);
            rollTarget = -3f;
            breath = 2.2f;                               // fast, scared breathing
        }));
        shake = 0.3f;
        StartCoroutine(FlickerHallLights(stareTime - 1.3f));
        yield return new WaitForSeconds(stareTime - 1.3f);

        // ---- 4. he CHARGES - the eyes stay on his face the whole way ----
        if (heartbeat != null)
        {
            heartbeat.volume = 0f;
            heartbeat.Play();
            StartCoroutine(FadeAudio(heartbeat, 0.9f, 0.8f));
        }
        fovTarget = baseFov * 1.08f;         // wide, so he stays in the picture as he comes close
        shake = 0.55f;
        lookSharpness = 7f;
        Kick(new Vector3(4f, 0f, -3f), 0.05f);           // jolt of panic
        bob = 1f;                                        // stumbling backwards: the view bobs with every step
        rollTarget = 6f;                                 // dutch tilt grows as he comes
        offsetTarget = new Vector3(0f, -0.1f, -0.2f);
        if (enemyAI != null && stopPoint != null)
        {
            Transform retreat = enemyRetreatPoint != null ? enemyRetreatPoint : enemyTeleportPoint;
            enemyAI.cinematicStopDistance = 0.4f;
            enemyAI.cinematicFlinchDuration = screamTime;
            enemyAI.cinematicRunSpeedMultiplier = 1.15f;
            enemyAI.cinematicRetreatSpeedMultiplier = 1.7f;
            bool runState = PlayEnemyState(enemyRunState, 0.2f);
            enemyAI.PlayCinematicEscape(stopPoint, retreat, runState ? "" : enemyAnimationTriggerName);
        }
        StartCoroutine(StumbleBack());

        float timeout = Time.time + maxChargeTime;
        while (Time.time < timeout)
        {
            if (enemyAI == null || enemyAI.CinematicHasArrived) break;
            if (Vector3.Distance(Flat(enemyAI.transform.position), Flat(stopPoint.position)) < 0.6f) break;
            yield return null;
        }

        // ---- 5. LIGHT: daylight bursts in through the cracks of the closed gate behind the player ----
        if (PlayEnemyState(enemyLightHitState, 0.1f))                   // the light hits him - he staggers back...
            StartCoroutine(EnemyStateThen(enemyLightHitState, enemyAgonyState, 0.25f));   // ...and burns, writhing
        StartCoroutine(SunBurstRoutine());
        StartCoroutine(SlowMoBeat());
        StartCoroutine(BurnRoutine());
        PlayCue(sunBurstSound, 1f, 1.6f, 0.25f);
        // the sun BURNS him: a pain shriek the instant it hits, then a long villain scream while he writhes, flames and sizzling skin
        PlayFromEnemy(burnShriek, 1f);
        PlayFromEnemy(burnScream, 0.9f, 0.85f);
        PlayCue(burnFire, 0.55f, screamTime + 1.6f, 1.2f);
        if (sizzle != null) { StartCoroutine(PlayCueAfter(0.15f, sizzle, 0.8f, 0.7f, 0.1f)); StartCoroutine(PlayCueAfter(0.9f, sizzle, 0.7f, 0.7f, 0.1f)); StartCoroutine(PlayCueAfter(1.5f, sizzle, 0.6f, 0.7f, 0.1f)); }
        // the player flinches away from the blinding light behind him...
        Kick(new Vector3(-10f, -24f, -9f), 0.1f);
        bob = 0f;
        shake = 0.8f;
        lookSharpness = 8f;
        // ...then, slumped down against the gate, looks up at the burning monster - low angle, slow push onto his face
        offsetTarget = new Vector3(0.05f, -0.42f, -0.18f);
        rollTarget = -5f;
        fovTarget = baseFov * 0.72f;
        StartCoroutine(After(screamTime * 0.5f, () => { fovTarget = baseFov * 0.6f; rollTarget = 3f; }));
        yield return new WaitForSeconds(screamTime);

        // ---- 6. he flees, the darkness swallows him ----
        fovTarget = baseFov * 0.78f;         // follow him down the hall
        shake = 0.3f;
        lookSharpness = 3.5f;
        breath = 1.6f;
        rollTarget = 7f;                                  // leaning out to watch him go
        offsetTarget = new Vector3(0.22f, -0.26f, -0.1f);
        StartCoroutine(After(0.9f, () => { fovTarget = baseFov * 0.68f; shake = 0.15f; }));
        if (heartbeat != null) StartCoroutine(FadeAudio(heartbeat, 0f, 2.5f));
        PlayEnemyState(enemyRunState, 0.15f);   // turns and runs from the light
        PlayFromEnemy(fleeHiss, 1f);             // hisses at the light as he turns
        PlayFromEnemy(fleeCry, 0.95f, 0.35f);    // and runs away wailing, terrified of the sun
        PlayFromEnemy(fleeGroan, 0.6f, 0.5f);
        StartCoroutine(KillHallLightsBehindHim(1.6f));
        yield return FleeIntoDarkness();        // runs until the dark swallows him (never stands stuck in view)
        if (bulb != null) bulb.enabled = false;
        StartCoroutine(CutEnemyVoices(0.35f));  // his wailing is swallowed by the dark at once
        if (enemyAI != null) enemyAI.gameObject.SetActive(false);

        // ---- 7. silence... then the player turns to the light at the gate ----
        yield return new WaitForSeconds(0.6f);
        freeWorldRot = aimRot;
        camMode = CamMode.Free;
        shake = 0.06f;
        offsetTarget = Vector3.zero;   // he gets back up
        rollTarget = 0f;
        breath = 1f;
        yield return TurnTowardsLight(2.2f);  // ends in warm white
        yield return new WaitForSeconds(0.5f);

        // ---- 8. black + THE END ----
        // ENDINGS (FR_54 - FR_56): decide which ending this run earned from the player's choices, BEFORE the save is wiped
        GameProgress.RunSummary runSummary = null;
        if (TestRun) finalMessage = "TEST RUN - nothing was saved";
        else
        {
            ObjectiveTracker.MarkEscaped();
            runSummary = GameProgress.FinishRun(true);
            finalMessage = runSummary.title.Replace("ENDING: ", "") + "  -  " + runSummary.tagline;
        }

        float startVolume = AudioListener.volume;
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.alpha = 1f;
        }
        yield return FadeFlash(new Color(1f, 0.97f, 0.9f), 1f, 0f, 1.0f);   // white -> black, quickly
        running = false;

        if (endingText != null)
        {
            endingText.gameObject.SetActive(true);
            endingText.alignment = TextAlignmentOptions.Center;
            endingText.fontSize = Mathf.Max(endingText.fontSize, 96f);
            endingText.text = string.IsNullOrEmpty(finalMessage)
                ? "THE END"
                : "THE END\n<size=35%><alpha=#AA>" + finalMessage + "</size>";

            Color tc = endingText.color;
            for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
            {
                tc.a = t / 2f;
                endingText.color = tc;
                AudioListener.volume = Mathf.Lerp(startVolume, 0.25f, t / 2f);
                yield return null;
            }
            tc.a = 1f;
            endingText.color = tc;
        }
        yield return new WaitForSecondsRealtime(5f);

        if (TestRun)
        {
            // editor test: keep the save, stop here
            AudioListener.volume = startVolume;
            Time.timeScale = 1f;
            TestRun = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            yield break;
        }

        // ---- 8b. ending summary: which ending, why, and the run statistics (FR_56) ----
        GameUI.ShowEndingSummary(runSummary);
        while (GameUI.IsSummaryOpen) yield return null;

        // ---- 9. game complete: wipe save, back to main menu ----
        if (SaveManager.instance != null) SaveManager.instance.DeleteSave();
        IntroState.ResetForNewGame();

        AudioListener.volume = startVolume;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    // ------------------------------------------------------------------ player
    private void PlacePlayerAtGate()
    {
        if (player == null) return;

        CharacterController cc = player.GetComponent<CharacterController>();
        // stand inside, behind the door, facing the hall; if something (an open door leaf, furniture) is in the way, step further in
        Vector3 standPos = Vector3.zero;
        float[] depths = { playerDepth, playerDepth + 0.4f, playerDepth + 0.8f, playerDepth + 1.2f };
        foreach (float d in depths)
        {
            standPos = new Vector3(gatePos.x, player.position.y, gatePos.z) + inward * d;
            playerDepth = d;
            if (cc == null || !IsBlocked(standPos, cc)) break;
        }
        if (cc != null) cc.enabled = false;
        player.position = standPos;
        player.rotation = Quaternion.LookRotation(inward, Vector3.up); // facing back into the hall
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;

        // Hide the first-person body (the camera turns a lot - it would clip through it)
        foreach (Renderer r in player.GetComponentsInChildren<Renderer>()) r.enabled = false;
    }

    private bool IsBlocked(Vector3 pos, CharacterController cc)
    {
        float r = cc.radius * 0.95f;
        Vector3 center = pos + cc.center;
        Vector3 up = Vector3.up * Mathf.Max(0f, cc.height * 0.5f - r);
        foreach (Collider c in Physics.OverlapCapsule(center - up + Vector3.up * 0.05f, center + up, r, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!c.transform.IsChildOf(player)) return true;
        }
        return false;
    }

    private IEnumerator StumbleBack()
    {
        if (player == null) yield break;
        CharacterController cc = player.GetComponent<CharacterController>();
        yield return new WaitForSeconds(0.5f);
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            Vector3 delta = outward * (0.6f * Time.deltaTime / 0.7f); // a small step back towards the door
            if (cc != null && cc.enabled) cc.Move(delta + Vector3.down * 0.01f); else player.position += delta;
            yield return null;
        }
    }

    private IEnumerator TurnTowardsLight(float duration)
    {
        if (player == null || playerCam == null) yield break;
        Quaternion start = freeWorldRot;
        Vector3 f = start * Vector3.forward; f.y = 0f; if (f.sqrMagnitude < 0.001f) f = inward;
        float side = Vector3.SignedAngle(f, outward, Vector3.up) >= 0f ? 1f : -1f;
        Quaternion glance = Quaternion.LookRotation(Quaternion.AngleAxis(side * 70f, Vector3.up) * f.normalized, Vector3.up) * Quaternion.Euler(8f, 0f, 0f);
        Quaternion end = Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(-6f, 0f, 0f);
        if (flashImage != null) flashImage.color = new Color(1f, 0.96f, 0.86f);
        float sunStart = sun != null ? sun.intensity : 0f;

        // a) slow glance over the shoulder (light catches the eye)
        bob = 0.35f;
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            freeWorldRot = Quaternion.Slerp(start, glance, Mathf.SmoothStep(0f, 1f, t / 0.7f));
            yield return null;
        }
        yield return new WaitForSeconds(0.2f);

        // b) then the full turn, stepping round towards the light, which swells until it fills the eyes
        Kick(new Vector3(0f, 0f, -side * 4f), 0f);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            freeWorldRot = Quaternion.Slerp(glance, end, k);
            player.rotation = Quaternion.LookRotation(Vector3.Slerp(inward, outward, k).sqrMagnitude > 0.001f ? Vector3.Slerp(inward, outward, k) : outward, Vector3.up);
            fovTarget = Mathf.Lerp(baseFov, baseFov * 0.85f, k);
            if (sun != null) sun.intensity = Mathf.Lerp(sunStart, sunBurst * 2f, k);
            if (flash != null) flash.alpha = Mathf.Clamp01((k - 0.45f) / 0.55f);
            yield return null;
        }
        bob = 0f;
        if (flash != null) flash.alpha = 1f;
    }

    private IEnumerator WalkOutside(float duration)
    {
        if (player == null) yield break;
        CharacterController cc = player.GetComponent<CharacterController>();
        for (float t = 0f; t < duration && running; t += Time.deltaTime)
        {
            Vector3 delta = outward * (1.6f * Time.deltaTime) + Vector3.down * 0.02f;
            if (cc != null && cc.enabled) cc.Move(delta); else player.position += delta;
            yield return null;
        }
    }

    // ------------------------------------------------------------------ enemy
    // ------------------------------------------------------------------ enemy animation
    private bool HasEnemyState(string state, out int hash)
    {
        hash = 0;
        Animator a = enemyAI != null ? enemyAI.enemyAnimator : null;
        if (a == null || string.IsNullOrEmpty(state) || a.runtimeAnimatorController == null) return false;
        hash = Animator.StringToHash(state);
        if (a.HasState(0, hash)) return true;
        hash = Animator.StringToHash("Base Layer." + state);
        return a.HasState(0, hash);
    }

    /// <summary>Cross-fades the joker into one of the escape states. False if the state does not exist (old controller).</summary>
    private bool PlayEnemyState(string state, float fade)
    {
        if (!HasEnemyState(state, out int hash)) return false;
        Animator a = enemyAI.enemyAnimator;
        a.ResetTrigger("Attack"); a.ResetTrigger("Tease");
        a.speed = 1f;
        a.CrossFadeInFixedTime(hash, fade, 0);
        return true;
    }

    /// <summary>When 'state' has (almost) finished, blends into 'next' - unless something else took over meanwhile.</summary>
    private IEnumerator EnemyStateThen(string state, string next, float fade)
    {
        if (!HasEnemyState(state, out int hash)) yield break;
        Animator a = enemyAI.enemyAnimator;
        float limit = Time.time + 12f;
        yield return null; yield return null;
        while (a != null && Time.time < limit)
        {
            if (!a.IsInTransition(0))
            {
                AnimatorStateInfo st = a.GetCurrentAnimatorStateInfo(0);
                bool inIt = st.shortNameHash == Animator.StringToHash(state) || st.fullPathHash == hash;
                if (!inIt) yield break;                                          // already moved on (e.g. he started running)
                if (st.normalizedTime >= 1f - fade / Mathf.Max(0.1f, st.length)) break;
            }
            yield return null;
        }
        if (a != null) PlayEnemyState(next, fade);
    }

    private void PrepareEnemy()
    {
        // where the sun stops him
        GameObject sp = new GameObject("EscapeEnemyStopPoint");
        // he stops 'enemyStopGap' metres in front of the player (the scene's door point is only used if it is even further in)
        float stopDepth = playerDepth + enemyStopGap;
        if (enemyDoorPoint != null) stopDepth = Mathf.Max(stopDepth, Vector3.Dot(enemyDoorPoint.position - gatePos, inward));
        Vector3 stopPos = gatePos + inward * stopDepth;
        stopPos.y = enemyDoorPoint != null ? enemyDoorPoint.position.y : gatePos.y;
        if (UnityEngine.AI.NavMesh.SamplePosition(stopPos, out UnityEngine.AI.NavMeshHit nh, 2f, UnityEngine.AI.NavMesh.AllAreas)) stopPos = nh.position;
        sp.transform.position = stopPos;
        stopPoint = sp.transform;

        if (enemyAI == null) return;

        enemyAI.EnsureActive();
        enemyAI.silentCinematicVoice = true; // every sound of the ending is played from here, in time with the animation

        // where he appears (the teleport point placed in the scene, deep in the hall)
        Vector3 spawn = enemyTeleportPoint != null ? enemyTeleportPoint.position : gatePos + inward * fallbackSpawnDepth;
        if (UnityEngine.AI.NavMesh.SamplePosition(spawn, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) spawn = hit.position;

        UnityEngine.AI.NavMeshAgent agent = enemyAI.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled) agent.Warp(spawn); else enemyAI.transform.position = spawn;
        enemyAI.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);

        Vector3 lookAt = player != null ? player.position : gatePos;
        enemyAI.HoldForCinematic(lookAt, null, false);
        PlayEnemyState(enemyIdleState, 0f);              // heavy breathing, staring at the player

        // A dying red bulb right above him, so the player can actually SEE him in the dark
        GameObject b = new GameObject("EscapeBulb");
        b.transform.SetParent(enemyAI.transform, false);
        b.transform.localPosition = new Vector3(0f, 2.6f, 0.9f);
        bulb = b.AddComponent<Light>();
        bulb.type = LightType.Point;
        bulb.color = bulbColor;
        bulb.range = 5.5f;
        bulb.intensity = 0f;
        bulb.shadows = LightShadows.None;
        bulbBase = 3.2f;
    }

    private Vector3 EnemyHead()
    {
        if (enemyAI == null) return gatePos + inward * 4f + Vector3.up * 1.6f;
        if (enemyAI.enemyAnimator != null && enemyAI.enemyAnimator.isHuman)
        {
            Transform head = enemyAI.enemyAnimator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) return head.position;
        }
        return enemyAI.transform.position + Vector3.up * 1.6f;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // ------------------------------------------------------------------ camera (the player's own eyes)
    private void SnapCameraToEnemy()
    {
        if (playerCam == null) return;
        trackInit = false;
        Quaternion want = FramedRotation(TrackPoint());
        playerCam.transform.rotation = want;
        aimRot = want;
    }

    /// <summary>Measures how high his head is above his feet while he stands still (used instead of the animated head bone).</summary>
    private void CaptureHeadHeight()
    {
        if (enemyAI == null) return;
        float h = EnemyHead().y - enemyAI.transform.position.y;
        if (h > 1.2f && h < 2.2f) headHeight = h;
    }

    /// <summary>
    /// CAMERA FIX: the camera used to follow his animated head bone. Now that he really runs, that bone bounces with every stride
    /// and the camera shook and bobbed. The target is now a steady point (feet + head height), smoothed.
    /// </summary>
    private Vector3 TrackPoint()
    {
        if (enemyAI == null) return gatePos + inward * 4f + Vector3.up * 1.6f;
        // aim a little below the head (face / chest), so his whole upper body is in the picture
        Vector3 raw = EnemyHead() - Vector3.up * 0.25f;
        if (!trackInit) { trackPos = raw; trackVel = Vector3.zero; trackInit = true; }
        trackPos = Vector3.SmoothDamp(trackPos, raw, ref trackVel, 0.14f);
        return trackPos;
    }

    /// <summary>
    /// Looks at the target, but never further than a person would turn: at most 55 degrees sideways of the hall direction
    /// and -20/+25 degrees up and down. So the camera can never swing round to the empty world outside.
    /// </summary>
    private Quaternion FramedRotation(Vector3 target)
    {
        Vector3 dir = target - playerCam.transform.position;
        Vector3 flat = new Vector3(dir.x, 0f, dir.z);
        if (flat.sqrMagnitude < 0.0001f) flat = inward;
        float yaw = Mathf.Clamp(Vector3.SignedAngle(inward, flat, Vector3.up), -55f, 55f);
        float pitch = Mathf.Clamp(Mathf.Atan2(dir.y, flat.magnitude) * Mathf.Rad2Deg, -35f, 25f);
        return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.LookRotation(inward, Vector3.up) * Quaternion.Euler(-pitch, 0f, 0f);
    }

    private void LateUpdate()
    {
        if (!running) return;

        // ---- he can NEVER get close enough to touch the player ----
        if (enemyAI != null && enemyAI.gameObject.activeInHierarchy)
        {
            Vector3 enemyPos = enemyAI.transform.position;
            float depth = Vector3.Dot(enemyPos - gatePos, inward);
            float minDepth = playerDepth + enemyMinGap;
            if (depth < minDepth)
            {
                Vector3 fixedPos = enemyPos + inward * (minDepth - depth);
                UnityEngine.AI.NavMeshAgent a = enemyAI.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (a != null && a.isOnNavMesh) a.Warp(fixedPos); else enemyAI.transform.position = fixedPos;
            }
        }

        FollowEnemyVoices();
        if (playerCam == null) return;

        // ---- eyes locked on his face, with a frightened handheld shake ----
        float t = Time.time * cameraShakeSpeed;
        float amp = Mathf.Max(cameraShakeAmount, 0.02f) * 40f * shake; // degrees
        Quaternion shakeRot = Quaternion.Euler(
            (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * amp,
            (Mathf.PerlinNoise(0.7f, t) - 0.5f) * amp,
            (Mathf.PerlinNoise(t * 0.5f, 5.1f) - 0.5f) * amp * 0.8f);

        if (camMode == CamMode.LookAtEnemy && enemyAI != null && enemyAI.gameObject.activeInHierarchy)
        {
            Quaternion want = FramedRotation(TrackPoint());
            aimRot = Quaternion.Slerp(aimRot, want, 1f - Mathf.Exp(-lookSharpness * Time.deltaTime));
        }
        else if (camMode == CamMode.Free)
        {
            aimRot = freeWorldRot;
        }

        if (camMode != CamMode.None)
        {
            Quaternion rig = UpdateRig(out Vector3 posOffset);
            playerCam.transform.rotation = aimRot * rig * shakeRot;
            ApplyCameraPosition(posOffset);
        }

        playerCam.fieldOfView = Mathf.Lerp(playerCam.fieldOfView, fovTarget, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
    }

    // ------------------------------------------------------------------ camera rig (body motion on top of the aim)
    // The aim says WHERE the player looks; the rig adds HOW his body moves: breathing, leaning, flinching (kicks),
    // stumbling steps (bob), the dutch tilt (roll) and sliding down against the gate (position offset).
    private Quaternion aimRot = Quaternion.identity;
    private Quaternion freeWorldRot = Quaternion.identity;
    private Vector3 camBaseLocal;
    private bool camBaseSet;
    private Vector3 offsetTarget, offsetCur, offsetVel;
    private float rollTarget, rollCur, rollVel;
    private float breath, bob, bobPhase;
    private Vector3 kickTarget, kickCur;   // pitch, yaw, roll (degrees)
    private float kickBackTarget, kickBackCur;

    private void CaptureCameraBase()
    {
        if (playerCam == null) return;
        if (!camBaseSet) { camBaseLocal = playerCam.transform.localPosition; camBaseSet = true; }
        offsetCur = offsetTarget = Vector3.zero; offsetVel = Vector3.zero;
        rollCur = rollTarget = 0f; kickCur = kickTarget = Vector3.zero; kickBackCur = kickBackTarget = 0f;
        aimRot = playerCam.transform.rotation;
    }

    /// <summary>A sudden jolt of the head (degrees pitch / yaw / roll, and metres backwards) that springs back by itself.</summary>
    private void Kick(Vector3 euler, float back)
    {
        kickTarget += euler;
        kickBackTarget += back;
    }

    private Quaternion UpdateRig(out Vector3 posOffset)
    {
        float dt = Time.deltaTime;
        // kicks: very fast attack, slower recovery
        kickCur = Vector3.Lerp(kickCur, kickTarget, 1f - Mathf.Exp(-22f * dt));
        kickTarget = Vector3.Lerp(kickTarget, Vector3.zero, 1f - Mathf.Exp(-3.5f * dt));
        kickBackCur = Mathf.Lerp(kickBackCur, kickBackTarget, 1f - Mathf.Exp(-22f * dt));
        kickBackTarget = Mathf.Lerp(kickBackTarget, 0f, 1f - Mathf.Exp(-3.5f * dt));

        offsetCur = Vector3.SmoothDamp(offsetCur, offsetTarget, ref offsetVel, 0.45f);
        rollCur = Mathf.SmoothDamp(rollCur, rollTarget, ref rollVel, 0.6f);

        // stumbling steps
        bobPhase += dt * 9.5f * Mathf.Clamp01(bob + 0.001f);
        float stepY = -Mathf.Abs(Mathf.Sin(bobPhase)) * 0.05f * bob;
        float stepX = Mathf.Sin(bobPhase) * 0.025f * bob;
        float stepRoll = Mathf.Sin(bobPhase) * 1.8f * bob;

        // breathing
        float br = Mathf.Sin(Time.time * (1.4f + breath * 0.9f)) * breath;
        float breathY = br * 0.012f;
        float breathPitch = br * 0.45f;

        posOffset = offsetCur + new Vector3(stepX, stepY + breathY, -kickBackCur);
        return Quaternion.Euler(kickCur.x + breathPitch, kickCur.y, kickCur.z + rollCur + stepRoll);
    }

    private void ApplyCameraPosition(Vector3 offset)
    {
        if (!camBaseSet || playerCam.transform.parent == null) return;
        Transform cam = playerCam.transform;
        Vector3 basePos = cam.parent.TransformPoint(camBaseLocal);
        Vector3 pos = basePos + Quaternion.LookRotation(inward, Vector3.up) * offset;
        // never push the eyes into the gate behind the player
        float depth = Vector3.Dot(pos - gatePos, inward);
        if (depth < 0.3f) pos += inward * (0.3f - depth);
        cam.position = pos;
    }

    private IEnumerator After(float delay, System.Action action)
    {
        yield return new WaitForSeconds(delay);
        if (running) action();
    }

    // ------------------------------------------------------------------ effects
    private void CreateFx()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.spatialBlend = 0f;
        sfx.playOnAwake = false;

        if (heartbeatLoop != null)
        {
            heartbeat = gameObject.AddComponent<AudioSource>();
            heartbeat.clip = heartbeatLoop;
            heartbeat.loop = true;
            heartbeat.spatialBlend = 0f;
            heartbeat.volume = 0.9f;
            heartbeat.playOnAwake = false;
        }

        // sunlight waiting outside the gate (behind the player), shining into the hall
        GameObject g = new GameObject("EscapeSun");
        g.transform.position = gatePos + outward * 3f + Vector3.up * 2.4f;
        Vector3 dir = inward + Vector3.down * 0.12f;
        g.transform.rotation = Quaternion.LookRotation(dir.normalized);
        sun = g.AddComponent<Light>();
        sun.type = LightType.Spot;
        sun.color = sunColor;
        sun.range = 26f;
        sun.spotAngle = 85f;
        sun.innerSpotAngle = 45f;
        sun.intensity = 0f;
        sun.shadows = LightShadows.None;

        // full-screen overlay used for blinks, the white-out and the walk into the light
        GameObject go = new GameObject("EscapeFlash");
        Canvas cv = go.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 400;
        flash = go.AddComponent<CanvasGroup>();
        flash.alpha = 0f;
        flash.blocksRaycasts = false;
        flash.interactable = false;

        GameObject img = new GameObject("Overlay");
        img.transform.SetParent(go.transform, false);
        flashImage = img.AddComponent<Image>();
        flashImage.color = Color.black;
        flashImage.raycastTarget = false;
        RectTransform rt = flashImage.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private Image flashImage;

    private IEnumerator FadeFlash(Color color, float from, float to, float duration)
    {
        if (flash == null) yield break;
        if (flashImage != null) flashImage.color = color;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            flash.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        flash.alpha = to;
    }

    private IEnumerator BulbFlicker()
    {
        // stutters on like a broken bulb, then keeps flickering
        float[] pattern = { 0.6f, 0f, 0.9f, 0.1f, 0f, 1f };
        foreach (float p in pattern)
        {
            if (bulb != null) bulb.intensity = bulbBase * p;
            yield return new WaitForSeconds(Random.Range(0.05f, 0.14f));
        }
        while (bulb != null && bulb.enabled)
        {
            float n = Mathf.PerlinNoise(Time.time * 9f, 1.7f);
            bulb.intensity = bulbBase * (n < 0.18f ? 0.05f : 0.7f + n * 0.5f);
            yield return null;
        }
    }

    private void CollectHallLights()
    {
        hallLights.Clear();
        foreach (Light l in FindObjectsByType<Light>())
        {
            if (l == null || l == sun || l == bulb || !l.enabled) continue;
            if (l.type != LightType.Point && l.type != LightType.Spot) continue;
            Vector3 rel = l.transform.position - gatePos;
            float depth = Vector3.Dot(rel, inward);
            float side = Vector3.Cross(inward, Flat(rel)).magnitude;
            if (depth > 0.5f && depth < 18f && side < 5f) hallLights.Add(l);
        }
    }

    private IEnumerator FlickerHallLights(float duration)
    {
        Dictionary<Light, float> baseIntensity = new Dictionary<Light, float>();
        foreach (Light l in hallLights) if (l != null) baseIntensity[l] = l.intensity;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            for (int i = 0; i < hallLights.Count; i++)
            {
                Light l = hallLights[i];
                if (l == null || !baseIntensity.ContainsKey(l)) continue;
                float n = Mathf.PerlinNoise(Time.time * 12f, i * 1.37f);
                l.intensity = baseIntensity[l] * (n < 0.35f ? 0.1f : 1f);
            }
            yield return null;
        }
        foreach (Light l in hallLights) if (l != null && baseIntensity.ContainsKey(l)) l.intensity = baseIntensity[l];
    }

    /// <summary>
    /// FLEE FIX: he used to run to the retreat point, get stuck there (still playing the run in place) and only vanish 2-3 s later.
    /// Now he sprints to the farthest reachable point down the hall, and the moment he is far enough, out of time,
    /// or stops moving, the darkness swallows him (his bulb dies and he is gone).
    /// </summary>
    private IEnumerator FleeIntoDarkness()
    {
        if (enemyAI == null) yield break;
        yield return null;   // let EnemyAI start its own retreat first, then take over

        UnityEngine.AI.NavMeshAgent agent = enemyAI.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
            float[] depths = { 30f, 24f, 18f, 14f, 10f };
            foreach (float d in depths)
            {
                Vector3 p = gatePos + inward * d;
                if (!UnityEngine.AI.NavMesh.SamplePosition(p, out UnityEngine.AI.NavMeshHit h, 4f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                if (agent.CalculatePath(h.position, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                { agent.SetPath(path); break; }
            }
            agent.isStopped = false;
            agent.speed = Mathf.Max(agent.speed, 7f);
            agent.acceleration = 40f;
            agent.angularSpeed = 900f;
            agent.autoBraking = false;
        }
        if (enemyAI.enemyAnimator != null) enemyAI.enemyAnimator.speed = 1.25f;   // panicked sprint

        float start = Time.time;
        Vector3 last = enemyAI.transform.position;
        float stuck = 0f;
        while (enemyAI != null && enemyAI.gameObject.activeInHierarchy)
        {
            yield return null;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 pos = enemyAI.transform.position;
            float speed = (Flat(pos) - Flat(last)).magnitude / dt;
            last = pos;
            float t = Time.time - start;
            if (t > 0.4f && speed < 1f) stuck += dt; else stuck = 0f;
            float dist = player != null ? Vector3.Distance(Flat(pos), Flat(player.position)) : 0f;
            bool arrived = agent != null && agent.isOnNavMesh && !agent.pathPending && agent.hasPath && agent.remainingDistance < 0.8f && t > 0.4f;
            if (stuck > 0.1f || arrived || dist > 13f || t > 2.2f) break;
        }

        // the darkness takes him: his bulb stutters out and he is gone in that blink
        if (bulb != null)
        {
            for (int i = 0; i < 3; i++)
            {
                bulb.enabled = false; yield return new WaitForSeconds(0.05f);
                bulb.enabled = true;  yield return new WaitForSeconds(0.04f);
            }
            bulb.enabled = false;
        }
    }

    private IEnumerator KillHallLightsBehindHim(float duration)
    {
        // nearest lights die first, so the blackness rolls down the hall after him
        hallLights.Sort((a, b) => Vector3.Dot(a.transform.position - gatePos, inward).CompareTo(Vector3.Dot(b.transform.position - gatePos, inward)));
        if (hallLights.Count == 0) yield break;

        float step = duration / (hallLights.Count + 1);
        foreach (Light l in hallLights)
        {
            yield return new WaitForSeconds(step);
            if (l != null) l.enabled = false;
        }
    }

    private IEnumerator SunBurstRoutine()
    {
        // a short sharp flash, then a warm, flickering light keeps him back (the hall stays readable - no white haze)
        if (flashImage != null) flashImage.color = new Color(1f, 0.97f, 0.88f);
        for (float t = 0f; t < 0.08f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.08f;
            if (sun != null) sun.intensity = Mathf.Lerp(0f, sunBurst * 1.6f, k);
            if (flash != null) flash.alpha = Mathf.Lerp(0f, 0.6f, k);
            yield return null;
        }
        for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
        {
            if (flash != null) flash.alpha = Mathf.Lerp(0.6f, 0f, t / 0.45f);
            if (sun != null) sun.intensity = Mathf.Lerp(sunBurst * 1.6f, sunBurst, t / 0.45f);
            yield return null;
        }
        if (flash != null) flash.alpha = 0f;
        while (running && camMode != CamMode.Free)
        {
            if (sun != null) sun.intensity = sunBurst * (0.85f + Mathf.PerlinNoise(Time.time * 10f, 3.1f) * 0.3f);
            yield return null;
        }
    }

    /// <summary>He burns where the light touches him: a flickering orange glow on his body while he shrieks and flees.</summary>
    private IEnumerator BurnRoutine()
    {
        if (enemyAI == null) yield break;
        GameObject g = new GameObject("EscapeBurnGlow");
        g.transform.SetParent(enemyAI.transform, false);
        g.transform.localPosition = new Vector3(0f, 0.4f, 0.4f);
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.45f, 0.12f); l.range = 3.2f; l.shadows = LightShadows.None;
        float end = Time.time + screamTime + fleeWatchTime;
        while (Time.time < end && l != null && enemyAI != null && enemyAI.gameObject.activeInHierarchy)
        {
            float fade = Mathf.Clamp01((end - Time.time) / fleeWatchTime);
            l.intensity = (3.5f + Mathf.PerlinNoise(Time.time * 18f, 0.4f) * 4f) * fade;
            yield return null;
        }
        if (g != null) Destroy(g);
    }

    private IEnumerator SlowMoBeat()
    {
        float oldFixed = Time.fixedDeltaTime;
        Time.timeScale = 0.3f;
        Time.fixedDeltaTime = oldFixed * 0.3f;
        yield return new WaitForSecondsRealtime(0.6f);
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            Time.timeScale = Mathf.Lerp(0.3f, 1f, t / 0.5f);
            Time.fixedDeltaTime = oldFixed * Time.timeScale;
            yield return null;
        }
        Time.timeScale = 1f;
        Time.fixedDeltaTime = oldFixed;
    }

    private IEnumerator FadeAudio(AudioSource src, float to, float duration)
    {
        float from = src.volume;
        for (float t = 0f; t < duration && src != null; t += Time.deltaTime)
        {
            src.volume = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        if (src != null) src.volume = to;
    }

    /// <summary>
    /// The fade screen and "THE END" text live INSIDE the HUD canvas (Canvas > blackdeathscreen > death text).
    /// Only the HUD parts are hidden; the branch holding the fade/ending text stays on.
    /// </summary>
    private AudioClip burnScream, burnShriek, burnFire, sizzle, fleeHiss, fleeCry, fleeGroan;
    private readonly List<AudioSource> enemyVoices = new List<AudioSource>();

    /// <summary>A sound that comes FROM the joker (3D): it follows him and gets quieter as he runs away.</summary>
    private AudioSource PlayFromEnemy(AudioClip clip, float volume, float delay = 0f)
    {
        if (clip == null || enemyAI == null) return null;
        GameObject g = new GameObject("JokerVoice3D");
        g.transform.position = EnemyHead();
        AudioSource s = g.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0.8f;                       // mostly 3D, a little 2D so it never disappears completely too early
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 2.5f; s.maxDistance = 26f;
        s.dopplerLevel = 0f;
        s.clip = clip; s.volume = volume;
        s.PlayDelayed(delay);
        enemyVoices.Add(s);
        Destroy(g, delay + clip.length + 0.2f);
        return s;
    }

    private void FollowEnemyVoices()
    {
        if (enemyAI == null || !enemyAI.gameObject.activeInHierarchy) return;
        Vector3 p = EnemyHead();
        for (int i = enemyVoices.Count - 1; i >= 0; i--)
        {
            if (enemyVoices[i] == null) { enemyVoices.RemoveAt(i); continue; }
            enemyVoices[i].transform.position = p;
        }
    }

    /// <summary>The darkness swallows him: every sound he makes is cut off (very quick fade).</summary>
    private IEnumerator CutEnemyVoices(float fade)
    {
        List<AudioSource> list = new List<AudioSource>(enemyVoices);
        enemyVoices.Clear();
        List<float> start = list.ConvertAll(a => a != null ? a.volume : 0f);
        for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] != null) list[i].volume = start[i] * (1f - t / fade);
            yield return null;
        }
        foreach (AudioSource a in list) if (a != null) Destroy(a.gameObject);
    }
    private AudioClip voiceLine;

    // ------------------------------------------------------------------ sound cues
    /// <summary>Plays only the first 'seconds' of a clip (then fades it out), on its own AudioSource so cues never cut each other off.</summary>
    private AudioSource PlayCue(AudioClip clip, float volume, float seconds, float fadeOut, float clipStart = 0f)
    {
        if (clip == null) return null;
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        src.clip = clip;
        src.volume = volume;
        src.time = Mathf.Clamp(clipStart, 0f, Mathf.Max(0f, clip.length - 0.05f));
        src.Play();
        StartCoroutine(StopCueAfter(src, seconds, fadeOut));
        return src;
    }

    private IEnumerator PlayCueAfter(float delay, AudioClip clip, float volume, float seconds, float fadeOut)
    {
        yield return new WaitForSecondsRealtime(delay); // real time: the slow-mo beat must not stretch the sound
        PlayCue(clip, volume, seconds, fadeOut);
    }

    private IEnumerator StopCueAfter(AudioSource src, float seconds, float fadeOut)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds - fadeOut));
        float start = src != null ? src.volume : 0f;
        for (float t = 0f; t < fadeOut && src != null; t += Time.unscaledDeltaTime)
        {
            src.volume = Mathf.Lerp(start, 0f, t / fadeOut);
            yield return null;
        }
        if (src != null)
        {
            src.Stop();
            Destroy(src);
        }
    }

    /// <summary>Fades out the level's own sounds (ambience, music, loops) so they do not mix with the ending.
    /// Kept: the player's breathing, the enemy's own source and the distance-based heartbeat of AudioManager (it rises as he charges).</summary>
    private IEnumerator DuckWorldAudio(float fade)
    {
        AudioManager am = FindAnyObjectByType<AudioManager>();
        AudioSource keep = am != null ? am.HeartbeatSource : null;

        List<AudioSource> targets = new List<AudioSource>();
        List<float> volumes = new List<float>();
        foreach (AudioSource a in FindObjectsByType<AudioSource>())
        {
            if (a == null || a == keep || !a.isPlaying || a.gameObject == gameObject) continue;
            if (enemyAI != null && a.transform.IsChildOf(enemyAI.transform)) continue;
            if (player != null && a.transform.IsChildOf(player)) continue;
            targets.Add(a);
            volumes.Add(a.volume);
        }

        for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Clamp01(t / fade);
            for (int i = 0; i < targets.Count; i++) if (targets[i] != null) targets[i].volume = volumes[i] * k;
            yield return null;
        }
        for (int i = 0; i < targets.Count; i++) if (targets[i] != null) targets[i].volume = 0f;
    }

    /// <summary>Use the horror sounds that ship with the project when nothing is assigned in the Inspector.</summary>
    private void LoadDefaultSounds()
    {
        if (appearSting == null) appearSting = Resources.Load<AudioClip>("HorrorAudio/freesound_community-horror-piano-hits-73998");
        if (sunBurstSound == null) sunBurstSound = Resources.Load<AudioClip>("HorrorAudio/biww-fire-burst-flame-sound-effect-561958");
        burnShriek = Resources.Load<AudioClip>("HorrorAudio/Joker_BurnShriek");
        burnScream = Resources.Load<AudioClip>("HorrorAudio/Joker_BurnScream");
        burnFire = Resources.Load<AudioClip>("HorrorAudio/Joker_BurnFire");
        sizzle = Resources.Load<AudioClip>("HorrorAudio/Joker_Sizzle");
        fleeHiss = Resources.Load<AudioClip>("HorrorAudio/Joker_FleeHiss");
        fleeCry = Resources.Load<AudioClip>("HorrorAudio/Joker_FleeCry");
        fleeGroan = Resources.Load<AudioClip>("HorrorAudio/Joker_FleeGroan");
        voiceLine = Resources.Load<AudioClip>("HorrorAudio/joker-2008-laugh");   // his laugh as he roars at the player (was "peek-a-boo")
        if (voiceLine == null) voiceLine = Resources.Load<AudioClip>("HorrorAudio/oliper18-scary-laugh-377526");
        if (playerStats != null) playerStats.muteBreathing = true;
    }

    private void HideGameplayHud()
    {
        Transform keepA = fadeCanvasGroup != null ? fadeCanvasGroup.transform : null;
        Transform keepB = endingText != null ? endingText.transform : null;

        foreach (Canvas c in FindObjectsByType<Canvas>())
        {
            if (c == null || !c.isRootCanvas) continue;
            if (!(c.name == "Canvas" || c.name.Contains("UI"))) continue;

            bool holdsEnding = (keepA != null && keepA.IsChildOf(c.transform)) || (keepB != null && keepB.IsChildOf(c.transform));
            if (!holdsEnding)
            {
                c.gameObject.SetActive(false);
                continue;
            }

            foreach (Transform child in c.transform)
            {
                bool keep = (keepA != null && keepA.IsChildOf(child)) || (keepB != null && keepB.IsChildOf(child));
                if (!keep) child.gameObject.SetActive(false);
            }
        }

        // The death picture shares the black screen - it must never pop up during the ending
        if (RespawnManager.instance != null && RespawnManager.instance.deathImage != null)
        {
            RespawnManager.instance.deathImage.gameObject.SetActive(false);
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
        if (endingText != null) endingText.text = "";
    }

    private void OnDisable()
    {
        // never leave the game stuck in slow-mo
        if (Time.timeScale != 1f) Time.timeScale = 1f;
    }
}
