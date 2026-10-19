using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// THE GRAND ESCAPE - end cinematic.
///
///  1. The player reaches the last stretch before the main gate. Control is taken away (his body is hidden).
///  2. Brother Silas bursts out of the darkness and SPRINTS for the gate. The camera runs backwards IN FRONT of his
///     face, tracking him at head height with a violent handheld shake.
///  3. Sunlight pours in through the open gate and gets brighter and brighter. The moment he SEES the sun he
///     freezes, screams, shields his eyes (white-out flash, FOV punch, slow-mo beat) ...
///  4. ... then he turns and RUNS back into the darkness in panic. The camera stays on him as he disappears.
///  5. Fade to black -> "THE END".
///
/// Everything is positioned relative to this object (the trigger at the gate) and the enemy teleport point,
/// so no scene re-wiring is needed. All the old inspector fields are kept.
/// </summary>
public class EscapeCinematicManager : MonoBehaviour
{
    [Header("Cinematic Elements")]
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
    public Transform enemyTeleportPoint; // Fallback spawn (inside the hall)
    public Transform enemyDoorPoint;     // (legacy - the stop point is computed from the gate)
    public Transform enemyRetreatPoint;  // Where he runs back to in the darkness
    public string enemyAnimationTriggerName = "Tease";

    [Header("Settings")]
    public string finalMessage = "THE END";   // (the code always shows THE END)
    public float fadeDuration = 2.5f;
    public float timeBeforeFade = 6f;

    // ---- Tuning (constants on purpose: scene-serialized values must never override them) ----
    private const float chaseTriggerDepth   = 5f;    // metres BEFORE the gate where the cinematic starts
    private const float enemySpawnDepth     = 12.5f; // how far back in the hall he bursts out from
    private const float enemyRunMultiplier  = 1.5f;  // x his chase speed
    private const float enemyStopDepth      = 3.0f;  // he freezes this far INSIDE the gate when he sees the sun
    private const float gateLimitDepth      = 0.6f;  // he can NEVER get closer to the gate line than this
    private const float enemyScreamDuration = 1.7f;  // how long he stands screaming before fleeing
    private const float enemyFleeMultiplier = 1.6f;  // flee speed (x chase speed)
    private const float fleeWatchTime       = 2.6f;  // how long we watch him run away before the fade
    private const float camLeadDistance     = 2.4f;  // camera runs this far in front of his face
    private const float camLeadClose        = 1.6f;  // ...and pushes in to this when he freezes
    private const float camHeight           = 1.5f;
    private const float runFov              = 58f;
    private const float punchFov            = 32f;
    private const float restFov             = 44f;
    private const float sunMaxBuildUp       = 14f;   // spot light intensity just before he sees it
    private const float sunBurst            = 55f;   // spot light intensity when he sees it
    private static readonly Color sunColor  = new Color(1f, 0.95f, 0.8f);

    private bool hasTriggered = false;
    private bool running = false;

    // geometry
    private Vector3 gatePos, inward, outward;
    private Transform player;
    private Camera mainCam;
    private Transform stopPoint;
    private Light sun;
    private CanvasGroup flash;

    // state
    private Vector3 camPos;
    private bool camInitialised = false;
    private float leadNow = camLeadDistance;
    private float fovNow = runFov;
    private float shakeBoost = 0f;
    private bool enemyArrived = false;   // = he has seen the sun
    private bool enemyFleeing = false;
    private float arrivalTime;
    private float sunBase = 0f;

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
        else if (enemyAI != null) inDir = enemyAI.transform.position - gatePos;
        inDir.y = 0f;
        if (inDir.sqrMagnitude < 0.01f) inDir = transform.forward;

        inward = inDir.normalized;
        outward = -inward;
    }

    /// <summary>Add a trigger volume in front of the gate (inside the building) so the cinematic starts a few metres early.</summary>
    private void ExtendTriggerBackwards()
    {
        BoxCollider bc = gameObject.AddComponent<BoxCollider>();
        bc.isTrigger = true;

        Vector3 localIn = transform.InverseTransformDirection(inward);
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Approximately(s.x, 0f) ? 1f : Mathf.Abs(s.x);
        float sy = Mathf.Approximately(s.y, 0f) ? 1f : Mathf.Abs(s.y);
        float sz = Mathf.Approximately(s.z, 0f) ? 1f : Mathf.Abs(s.z);

        float width = 3.4f, height = 3f;
        if (Mathf.Abs(localIn.z) >= Mathf.Abs(localIn.x))
        {
            float dir = Mathf.Sign(localIn.z);
            bc.size = new Vector3(width / sx, height / sy, chaseTriggerDepth / sz);
            bc.center = new Vector3(0f, 1f / sy, dir * (chaseTriggerDepth * 0.5f) / sz);
        }
        else
        {
            float dir = Mathf.Sign(localIn.x);
            bc.size = new Vector3(chaseTriggerDepth / sx, height / sy, width / sz);
            bc.center = new Vector3(dir * (chaseTriggerDepth * 0.5f) / sx, 1f / sy, 0f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!hasTriggered && other.CompareTag("Player"))
        {
            hasTriggered = true;
            StartCoroutine(PlayCinematicSequence(other.gameObject));
        }
    }

    // ------------------------------------------------------------------ main sequence
    private IEnumerator PlayCinematicSequence(GameObject playerObj)
    {
        Debug.Log("Starting Grand Escape Cinematic!");
        player = playerObj.transform;

        // ---- 1. take control away from the player and hide him (first-person body has no head) ----
        PlayerController pc = playerObj.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;

        mainCam = Camera.main;
        if (mainCam != null)
        {
            InteractionRaycast ir = mainCam.GetComponent<InteractionRaycast>();
            if (ir != null) ir.enabled = false;
        }

        Renderer[] playerRenderers = playerObj.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in playerRenderers) r.enabled = false;

        PlayerStats ps = playerObj.GetComponent<PlayerStats>();
        if (ps != null) ps.TriggerHeavyBreathing(10f);

        // ---- 2. hide gameplay HUD ----
        Canvas[] allCanvases = FindObjectsByType<Canvas>();
        foreach (Canvas c in allCanvases)
        {
            if (fadeCanvasGroup != null && (c.transform.IsChildOf(fadeCanvasGroup.transform) || c.transform == fadeCanvasGroup.transform)) continue;
            if (c.name == "Canvas" || c.name.Contains("UI")) c.gameObject.SetActive(false);
        }

        // ---- 3. the sun + white-out overlay ----
        CreateSun();
        CreateFlash();

        // ---- 4. enemy bursts out of the darkness and runs for the gate ----
        PrepareEnemy();

        // ---- 5. camera ----
        if (cinematicCamera != null)
        {
            if (mainCam != null)
            {
                AudioListener mainListener = mainCam.GetComponent<AudioListener>();
                if (mainListener != null) mainListener.enabled = false;
                cinematicCamera.depth = mainCam.depth + 10f;
                mainCam.enabled = false;
            }
            AudioListener cl = cinematicCamera.GetComponent<AudioListener>();
            if (cl == null) cl = cinematicCamera.gameObject.AddComponent<AudioListener>();
            cl.enabled = true;

            cinematicCamera.gameObject.SetActive(true);
            cinematicCamera.fieldOfView = fovNow;
        }

        ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
        if (pm != null) pm.StartEscapePhase();

        running = true;
        StartCoroutine(MovePlayerOutside());

        // ---- 6. wait until he sees the sun ----
        float timeout = Time.time + 14f;
        while (!enemyArrived && Time.time < timeout)
        {
            if (enemyAI != null && enemyAI.CinematicHasArrived) OnEnemySeesSun();
            yield return null;
        }
        if (!enemyArrived) OnEnemySeesSun(); // safety

        // ---- 7. he screams, then flees; we watch him disappear ----
        yield return new WaitForSeconds(enemyScreamDuration);
        enemyFleeing = true;
        yield return new WaitForSeconds(fleeWatchTime);

        // ---- 8. fade to black ----
        if (flash != null) StartCoroutine(FadeFlashOut());
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }
        running = false;

        // ---- 9. THE END ----
        if (endingText != null)
        {
            endingText.gameObject.SetActive(true);
            endingText.text = "THE END";
            endingText.alignment = TextAlignmentOptions.Center;
            endingText.fontSize = Mathf.Max(endingText.fontSize, 96f);
            Color tc = endingText.color; tc.a = 1f; endingText.color = tc;
        }
        yield return new WaitForSecondsRealtime(5f);

        // ---- 10. game complete: wipe save, back to main menu ----
        if (SaveManager.instance != null) SaveManager.instance.DeleteSave();
        IntroState.ResetForNewGame();

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    // ------------------------------------------------------------------ player (invisible, just moved out of the way)
    private IEnumerator MovePlayerOutside()
    {
        if (player == null) yield break;
        CharacterController cc = player.GetComponent<CharacterController>();
        Vector3 target = new Vector3(gatePos.x, player.position.y, gatePos.z) + outward * 4f;

        while (running)
        {
            Vector3 flat = target - player.position; flat.y = 0f;
            if (flat.magnitude < 0.25f) break;
            Vector3 delta = flat.normalized * 5f * Time.deltaTime;
            if (cc != null && cc.enabled) cc.Move(delta); else player.position += delta;
            yield return null;
        }
    }

    // ------------------------------------------------------------------ enemy
    private void PrepareEnemy()
    {
        if (enemyAI == null) return;

        // he freezes enemyStopDepth metres inside the gate when he sees the sun
        GameObject sp = new GameObject("EscapeEnemyStopPoint");
        Vector3 stopPos = gatePos + inward * enemyStopDepth;
        UnityEngine.AI.NavMeshHit nh;
        if (UnityEngine.AI.NavMesh.SamplePosition(stopPos, out nh, 2f, UnityEngine.AI.NavMesh.AllAreas)) stopPos = nh.position;
        sp.transform.position = stopPos;
        stopPoint = sp.transform;

        enemyAI.EnsureActive();

        // burst out of the darkness, far back in the hall (falls back to the old teleport point)
        Vector3 spawn = gatePos + inward * enemySpawnDepth;
        UnityEngine.AI.NavMeshAgent agent = enemyAI.GetComponent<UnityEngine.AI.NavMeshAgent>();
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(spawn, out hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
        {
            spawn = hit.position;
        }
        else if (enemyTeleportPoint != null)
        {
            spawn = enemyTeleportPoint.position;
            if (UnityEngine.AI.NavMesh.SamplePosition(spawn, out hit, 10f, UnityEngine.AI.NavMesh.AllAreas)) spawn = hit.position;
        }
        if (agent != null) agent.Warp(spawn); else enemyAI.transform.position = spawn;

        Vector3 faceDir = outward; faceDir.y = 0f;
        if (faceDir.sqrMagnitude > 0.01f) enemyAI.transform.rotation = Quaternion.LookRotation(faceDir);

        enemyAI.cinematicStopDistance = 0.35f;
        enemyAI.cinematicFlinchDuration = enemyScreamDuration;
        enemyAI.cinematicRunSpeedMultiplier = enemyRunMultiplier;
        enemyAI.cinematicRetreatSpeedMultiplier = enemyFleeMultiplier;
        Transform retreat = enemyRetreatPoint != null ? enemyRetreatPoint : stopPoint;
        enemyAI.PlayCinematicEscape(stopPoint, retreat, enemyAnimationTriggerName);
    }

    private void OnEnemySeesSun()
    {
        if (enemyArrived) return;
        enemyArrived = true;
        arrivalTime = Time.time;

        shakeBoost = 1f;
        StartCoroutine(FovPunch());
        StartCoroutine(SlowMoBeat());
        StartCoroutine(SunBurstRoutine());
    }

    // ------------------------------------------------------------------ camera (runs backwards in front of his face)
    private void LateUpdate()
    {
        if (!running || cinematicCamera == null || enemyAI == null) return;

        Vector3 enemyPos = enemyAI.transform.position;
        float depth = Vector3.Dot(enemyPos - gatePos, inward);

        // ---- he can NEVER get through the gate ----
        if (depth < gateLimitDepth)
        {
            Vector3 fixedPos = enemyPos + inward * (gateLimitDepth - depth);
            UnityEngine.AI.NavMeshAgent a = enemyAI.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (a != null && a.isOnNavMesh) a.Warp(fixedPos); else enemyAI.transform.position = fixedPos;
            enemyPos = fixedPos;
        }

        // ---- sun builds up as he approaches the gate ----
        if (!enemyArrived)
        {
            float k = Mathf.InverseLerp(11f, enemyStopDepth, depth);
            sunBase = Mathf.Lerp(0f, sunMaxBuildUp, k * k);
            if (sun != null) sun.intensity = sunBase;
            if (flash != null) flash.alpha = Mathf.Lerp(0f, 0.22f, k * k);
        }

        // ---- when he sees it he squares up to the light; once fleeing the AI turns him around ----
        if (enemyArrived && !enemyFleeing)
        {
            Quaternion face = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.z));
            enemyAI.transform.rotation = Quaternion.Slerp(enemyAI.transform.rotation, face, 8f * Time.deltaTime);
        }

        // ---- camera: ahead of his face (towards the gate), at head height ----
        if (enemyArrived) leadNow = Mathf.MoveTowards(leadNow, camLeadClose, 0.5f * Time.deltaTime);
        Vector3 target = enemyPos + outward * leadNow;
        target.y = enemyPos.y + camHeight;

        if (!camInitialised) { camPos = target; camInitialised = true; }
        else camPos = Vector3.Lerp(camPos, target, 1f - Mathf.Exp(-14f * Time.deltaTime));

        // handheld shake: violent while he runs, shudder when he sees the sun
        shakeBoost = Mathf.MoveTowards(shakeBoost, 0f, Time.deltaTime * 0.6f);
        float intensity = (enemyArrived ? cameraShakeAmount : cameraShakeAmount * 2.2f) + shakeBoost * 0.12f;
        float t = Time.time * cameraShakeSpeed;
        float ox = (Mathf.PerlinNoise(t, 0f) - 0.5f) * intensity;
        float oy = (Mathf.PerlinNoise(0f, t) - 0.5f) * intensity;
        float roll = (Mathf.PerlinNoise(t * 0.5f, 7.3f) - 0.5f) * ((enemyArrived ? 2f : 5f) + shakeBoost * 6f);
        Vector3 bob = new Vector3(ox, oy + (enemyArrived ? 0f : Mathf.Sin(Time.time * 13f) * 0.04f), 0f);
        cinematicCamera.transform.position = camPos + bob;

        // look at his face
        Vector3 lookTarget = enemyPos + Vector3.up * 1.65f;
        Vector3 lookDir = lookTarget - cinematicCamera.transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion want = Quaternion.LookRotation(lookDir.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
            cinematicCamera.transform.rotation = Quaternion.Slerp(cinematicCamera.transform.rotation, want, 1f - Mathf.Exp(-12f * Time.deltaTime));
        }

        cinematicCamera.fieldOfView = fovNow;
    }

    // ------------------------------------------------------------------ effects
    private void CreateSun()
    {
        // sunlight pouring in through the open gate (comes from outside, shines into the hall)
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
    }

    private void CreateFlash()
    {
        GameObject go = new GameObject("EscapeSunFlash");
        Canvas cv = go.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 400;
        flash = go.AddComponent<CanvasGroup>();
        flash.alpha = 0f;
        flash.blocksRaycasts = false;
        flash.interactable = false;

        GameObject img = new GameObject("White");
        img.transform.SetParent(go.transform, false);
        Image im = img.AddComponent<Image>();
        im.color = new Color(1f, 0.97f, 0.88f, 1f);
        im.raycastTarget = false;
        RectTransform rt = im.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private IEnumerator SunBurstRoutine()
    {
        // he SEES the sun: blinding white-out, then it settles to a bright glare
        float t = 0f;
        while (t < 0.08f)
        {
            t += Time.unscaledDeltaTime;
            float k = t / 0.08f;
            if (sun != null) sun.intensity = Mathf.Lerp(sunBase, sunBurst * 1.5f, k);
            if (flash != null) flash.alpha = Mathf.Lerp(flash.alpha, 0.95f, k);
            yield return null;
        }
        t = 0f;
        while (t < 0.9f)
        {
            t += Time.unscaledDeltaTime;
            float k = t / 0.9f;
            if (flash != null) flash.alpha = Mathf.Lerp(0.95f, 0.14f, k);
            yield return null;
        }
        // nervous flicker of the sun while he screams and flees
        while (running)
        {
            if (sun != null) sun.intensity = sunBurst * (0.85f + Mathf.PerlinNoise(Time.time * 14f, 3.1f) * 0.3f);
            yield return null;
        }
    }

    private IEnumerator FadeFlashOut()
    {
        float start = flash != null ? flash.alpha : 0f;
        float t = 0f;
        while (t < 1f && flash != null)
        {
            t += Time.unscaledDeltaTime;
            flash.alpha = Mathf.Lerp(start, 0f, t);
            yield return null;
        }
    }

    private IEnumerator FovPunch()
    {
        // vertigo zoom: snap in hard on his terrified face, then slowly widen out as he flees
        float start = fovNow;
        float t = 0f;
        while (t < 0.2f) { t += Time.unscaledDeltaTime; fovNow = Mathf.Lerp(start, punchFov, t / 0.2f); yield return null; }
        t = 0f;
        while (t < 4f) { t += Time.unscaledDeltaTime; fovNow = Mathf.Lerp(punchFov, restFov, Mathf.SmoothStep(0f, 1f, t / 4f)); yield return null; }
    }

    private IEnumerator SlowMoBeat()
    {
        float oldFixed = Time.fixedDeltaTime;
        Time.timeScale = 0.25f;
        Time.fixedDeltaTime = oldFixed * 0.25f;
        yield return new WaitForSecondsRealtime(0.5f);
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(0.25f, 1f, t / 0.4f);
            Time.fixedDeltaTime = oldFixed * Time.timeScale;
            yield return null;
        }
        Time.timeScale = 1f;
        Time.fixedDeltaTime = oldFixed;
    }

    private void OnDisable()
    {
        // never leave the game stuck in slow-mo
        if (Time.timeScale != 1f) Time.timeScale = 1f;
    }
}
