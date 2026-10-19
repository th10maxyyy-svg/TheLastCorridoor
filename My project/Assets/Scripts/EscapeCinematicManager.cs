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
    private const float playerStandOutside = 0.8f; // player stands this far OUTSIDE the gate line
    private const float fallbackSpawnDepth = 9f;   // if no teleport point: metres inside the hall
    private const float fallbackStopDepth = 2.2f;  // if no door point: where the sun stops him
    private const float gateLimitDepth = 0.9f;     // he can NEVER get closer to the gate line than this
    private const float stareTime = 3.4f;          // how long he just stands and stares before charging
    private const float maxChargeTime = 6f;        // safety timeout for the charge
    private const float screamTime = 1.8f;         // burning/screaming at the light
    private const float fleeWatchTime = 2.6f;      // watching him vanish into the dark
    private const float sunBurst = 60f;
    private static readonly Color sunColor = new Color(1f, 0.95f, 0.82f);
    private static readonly Color bulbColor = new Color(1f, 0.28f, 0.18f);

    private bool hasTriggered = false;
    private bool running = false;

    // geometry
    private Vector3 gatePos, inward, outward;
    private Transform stopPoint;

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
        if (!(other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)) return;

        PlayerStats ps = other.GetComponent<PlayerStats>();
        if (ps != null && ps.IsDead()) return;

        hasTriggered = true;
        StartCoroutine(PlayCinematicSequence(other.gameObject));
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
            playerStats.TriggerHeavyBreathing(30f);
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

        ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
        if (pm != null) pm.StartEscapePhase();

        // ---- 2. blink: when the eyes open he has turned round, back towards the dark hall ----
        yield return FadeFlash(Color.black, 0f, 1f, 0.25f);
        PlacePlayerAtGate();
        PrepareEnemy();
        CollectHallLights();
        running = true;
        camMode = CamMode.LookAtEnemy;
        lookSharpness = 2.2f;
        SnapCameraToEnemy();
        yield return new WaitForSeconds(0.35f);
        yield return FadeFlash(Color.black, 1f, 0f, 0.9f);

        // ---- 3. the bulb flickers on - he is standing there, staring ----
        StartCoroutine(BulbFlicker());
        if (appearSting != null) sfx.PlayOneShot(appearSting, 1f);
        fovTarget = baseFov * 0.72f;   // slow dolly-zoom onto him
        shake = 0.15f;
        yield return new WaitForSeconds(1.2f);

        if (enemyAI != null)
        {
            enemyAI.PlayScream();
            if (!string.IsNullOrEmpty(enemyAnimationTriggerName) && enemyAI.enemyAnimator != null)
                enemyAI.enemyAnimator.SetTrigger(enemyAnimationTriggerName);
        }
        shake = 0.35f;
        StartCoroutine(FlickerHallLights(stareTime - 1.2f));
        yield return new WaitForSeconds(stareTime - 1.2f);

        // ---- 4. he CHARGES ----
        if (heartbeat != null) heartbeat.Play();
        fovTarget = baseFov * 0.95f;
        shake = 0.8f;
        lookSharpness = 6f;
        if (enemyAI != null && stopPoint != null)
        {
            Transform retreat = enemyRetreatPoint != null ? enemyRetreatPoint : enemyTeleportPoint;
            enemyAI.cinematicStopDistance = 0.4f;
            enemyAI.cinematicFlinchDuration = screamTime;
            enemyAI.cinematicRunSpeedMultiplier = 1.6f;
            enemyAI.cinematicRetreatSpeedMultiplier = 1.7f;
            enemyAI.PlayCinematicEscape(stopPoint, retreat, enemyAnimationTriggerName);
        }
        StartCoroutine(StumbleBack());

        float timeout = Time.time + maxChargeTime;
        while (Time.time < timeout)
        {
            if (enemyAI == null || enemyAI.CinematicHasArrived) break;
            // Failsafe: he got close enough even if the agent never "arrived"
            if (enemyAI != null && Vector3.Distance(Flat(enemyAI.transform.position), Flat(stopPoint.position)) < 0.6f) break;
            yield return null;
        }

        // ---- 5. SUNLIGHT ----
        StartCoroutine(SunBurstRoutine());
        StartCoroutine(SlowMoBeat());
        if (sunBurstSound != null) sfx.PlayOneShot(sunBurstSound, 1f);
        if (burnScream != null) sfx.PlayOneShot(burnScream, 0.9f); // he burns in the light
        else if (enemyAI != null) enemyAI.PlayScream();
        fovTarget = baseFov * 0.55f; // punch in on his burning face
        shake = 1f;
        lookSharpness = 9f;
        yield return new WaitForSeconds(screamTime);

        // ---- 6. he flees, the darkness swallows him ----
        fovTarget = baseFov * 0.8f;
        shake = 0.35f;
        lookSharpness = 3f;
        if (heartbeat != null) StartCoroutine(FadeAudio(heartbeat, 0f, 2.5f));
        StartCoroutine(KillHallLightsBehindHim(fleeWatchTime));
        yield return new WaitForSeconds(fleeWatchTime);
        if (bulb != null) bulb.enabled = false;
        if (enemyAI != null) enemyAI.gameObject.SetActive(false); // gone into the dark

        // ---- 7. turn to the light and walk out ----
        if (playerCam != null) freeCamLocalRot = playerCam.transform.localRotation * Quaternion.Inverse(lastShake);
        camMode = CamMode.Free;
        shake = 0.1f;
        yield return TurnTowardsLight(2.2f);

        StartCoroutine(WalkOutside(3f));
        if (flash != null) flash.alpha = 1f;
        yield return new WaitForSeconds(1.8f); // hold on the white light

        // ---- 8. black + THE END ----
        float startVolume = AudioListener.volume;
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.alpha = 1f;
        }
        yield return FadeFlash(new Color(1f, 0.97f, 0.9f), 1f, 0f, fadeDuration);
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
        Vector3 standPos = new Vector3(gatePos.x, player.position.y, gatePos.z) + outward * playerStandOutside;
        if (cc != null && IsBlocked(standPos, cc))
        {
            // A closed gate / wall is there - stand just inside instead
            standPos = new Vector3(gatePos.x, player.position.y, gatePos.z) + inward * 0.3f;
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
            Vector3 delta = outward * (0.9f * Time.deltaTime / 0.7f);
            if (cc != null && cc.enabled) cc.Move(delta + Vector3.down * 0.01f); else player.position += delta;
            yield return null;
        }
    }

    private IEnumerator TurnTowardsLight(float duration)
    {
        if (player == null || playerCam == null) yield break;

        Quaternion rootStart = player.rotation;
        Quaternion rootEnd = Quaternion.LookRotation(outward, Vector3.up);
        Quaternion camStart = freeCamLocalRot;
        Quaternion camEnd = Quaternion.Euler(-6f, 0f, 0f); // look slightly up into the sky

        // There is NOTHING built outside the asylum (black fog, no sky). So as he turns, the world outside becomes
        // blinding daylight: fog + background fade to warm white and the screen whites out before the void is visible.
        Color fogStart = RenderSettings.fogColor;
        float densityStart = RenderSettings.fogDensity;
        Color daylight = new Color(1f, 0.96f, 0.86f);
        playerCam.clearFlags = CameraClearFlags.SolidColor;
        Color bgStart = playerCam.backgroundColor;
        if (flashImage != null) flashImage.color = daylight;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            player.rotation = Quaternion.Slerp(rootStart, rootEnd, k);
            freeCamLocalRot = Quaternion.Slerp(camStart, camEnd, k);
            playerCam.fieldOfView = Mathf.Lerp(playerCam.fieldOfView, baseFov, Time.deltaTime * 2f);
            RenderSettings.fogColor = Color.Lerp(fogStart, daylight, k);
            RenderSettings.fogDensity = Mathf.Lerp(densityStart, Mathf.Max(densityStart, 0.12f), k);
            playerCam.backgroundColor = Color.Lerp(bgStart, daylight, k);
            // fully white before he is facing outside
            if (flash != null) flash.alpha = Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(k * 1.6f));
            yield return null;
        }
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
    private void PrepareEnemy()
    {
        // where the sun stops him
        GameObject sp = new GameObject("EscapeEnemyStopPoint");
        Vector3 stopPos = enemyDoorPoint != null ? enemyDoorPoint.position : gatePos + inward * fallbackStopDepth;
        float stopDepth = Vector3.Dot(stopPos - gatePos, inward);
        if (stopDepth < gateLimitDepth + 0.2f) stopPos += inward * (gateLimitDepth + 0.2f - stopDepth);
        if (UnityEngine.AI.NavMesh.SamplePosition(stopPos, out UnityEngine.AI.NavMeshHit nh, 2f, UnityEngine.AI.NavMesh.AllAreas)) stopPos = nh.position;
        sp.transform.position = stopPos;
        stopPoint = sp.transform;

        if (enemyAI == null) return;

        enemyAI.EnsureActive();

        // where he appears (the teleport point placed in the scene, deep in the hall)
        Vector3 spawn = enemyTeleportPoint != null ? enemyTeleportPoint.position : gatePos + inward * fallbackSpawnDepth;
        if (UnityEngine.AI.NavMesh.SamplePosition(spawn, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) spawn = hit.position;

        UnityEngine.AI.NavMeshAgent agent = enemyAI.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled) agent.Warp(spawn); else enemyAI.transform.position = spawn;
        enemyAI.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);

        Vector3 lookAt = player != null ? player.position : gatePos;
        enemyAI.HoldForCinematic(lookAt, null, false);

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
        Vector3 dir = EnemyHead() - playerCam.transform.position;
        if (dir.sqrMagnitude > 0.001f) playerCam.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void LateUpdate()
    {
        if (!running) return;

        // ---- he can NEVER get through the gate ----
        if (enemyAI != null && enemyAI.gameObject.activeInHierarchy)
        {
            Vector3 enemyPos = enemyAI.transform.position;
            float depth = Vector3.Dot(enemyPos - gatePos, inward);
            if (depth < gateLimitDepth)
            {
                Vector3 fixedPos = enemyPos + inward * (gateLimitDepth - depth);
                UnityEngine.AI.NavMeshAgent a = enemyAI.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (a != null && a.isOnNavMesh) a.Warp(fixedPos); else enemyAI.transform.position = fixedPos;
            }
        }

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
            Vector3 dir = EnemyHead() - playerCam.transform.position;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
                Quaternion smooth = Quaternion.Slerp(playerCam.transform.rotation * Quaternion.Inverse(lastShake), want, 1f - Mathf.Exp(-lookSharpness * Time.deltaTime));
                playerCam.transform.rotation = smooth * shakeRot;
                lastShake = shakeRot;
            }
        }
        else if (camMode == CamMode.Free)
        {
            playerCam.transform.localRotation = freeCamLocalRot * shakeRot;
            lastShake = Quaternion.identity;
        }

        playerCam.fieldOfView = Mathf.Lerp(playerCam.fieldOfView, fovTarget, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
    }

    private Quaternion lastShake = Quaternion.identity;
    private Quaternion freeCamLocalRot = Quaternion.identity; // camera rotation without shake (Free mode)

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
        // blinding white-out, then it settles to a hard glare that keeps him back
        if (flashImage != null) flashImage.color = new Color(1f, 0.97f, 0.88f);
        for (float t = 0f; t < 0.08f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.08f;
            if (sun != null) sun.intensity = Mathf.Lerp(0f, sunBurst * 1.5f, k);
            if (flash != null) flash.alpha = Mathf.Lerp(0f, 0.95f, k);
            yield return null;
        }
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
        {
            if (flash != null) flash.alpha = Mathf.Lerp(0.95f, 0.15f, t);
            yield return null;
        }
        while (running)
        {
            if (sun != null) sun.intensity = sunBurst * (0.85f + Mathf.PerlinNoise(Time.time * 14f, 3.1f) * 0.3f);
            yield return null;
        }
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
    private AudioClip burnScream;

    /// <summary>Use the horror sounds that ship with the project when nothing is assigned in the Inspector.</summary>
    private void LoadDefaultSounds()
    {
        if (appearSting == null) appearSting = Resources.Load<AudioClip>("HorrorAudio/freesound_community-horror-piano-hits-73998");
        if (sunBurstSound == null) sunBurstSound = Resources.Load<AudioClip>("HorrorAudio/biww-fire-burst-flame-sound-effect-561958");
        burnScream = Resources.Load<AudioClip>("HorrorAudio/dragon-studio-female-zombie-screams-324744");
        if (playerStats != null) playerStats.TriggerHeavyBreathing(30f);
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
