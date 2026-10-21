using UnityEngine;
using UnityEngine.AI;

public class CinematicAmbush : MonoBehaviour
{
    [Header("Enemy Setup")]
    [Tooltip("The Enemy that will ambush the player")]
    public EnemyAI enemy;
    [Tooltip("Where the enemy should spawn when the ambush starts")]
    public Transform ambushSpawnPoint;

    [Header("Tease / Jumpscare Settings")]
    [Tooltip("The scary sound to play right when he spawns!")]
    public AudioClip ambushScream;
    [Tooltip("If true, plays a Jumpscare animation (Tease) instead of chasing.")]
    public bool useTeaseInsteadOfChase = true;
    [Tooltip("How long the enemy yells/teases before walking away")]
    public float teaseDuration = 3f;
    [Tooltip("Optional: Where the enemy walks away to after the jumpscare")]
    public Transform teaseLeaveTarget;

    [Header("Drag to Basement (Story Transition)")]
    [Tooltip("Agar true hai, to enemy player ko maar kar game over nahi karega, balke basement mein phenk dega!")]
    public bool isBasementTransition = false;
    [Tooltip("Wo khali jagah (Empty GameObject) jahan player basement mein giray ga")]
    public Transform basementSpawnPoint;

    private AudioSource audioSource;
    private bool hasAmbushed = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // WORLD STATE: respawn ke baad wahi jumpscare / basement drag dobara na chale
        saveFlag = "Ambush_" + SaveManager.GetSceneObjectId(transform);
        if (SaveManager.instance != null && SaveManager.instance.HasFlag(saveFlag)) hasAmbushed = true;

        // RESUME FIX: the game was closed after the Guard Room was unlocked but BEFORE the player reached the basement
        // (e.g. during the jumpscare). The door is open but the event never finished - replay it as soon as the player
        // comes back to that door, instead of leaving him upstairs with the story broken.
        if (isBasementTransition && !hasAmbushed && SaveManager.instance != null)
        {
            pendingDoor = FindStoryDoor();
            if (pendingDoor != null && SaveManager.instance.IsDoorUnlocked(pendingDoor.DoorID))
            {
                pendingRetrigger = true;
                Debug.Log("[Ambush] Resume: the basement event was not finished - it will replay at " + pendingDoor.name);
            }
        }
    }

    private bool pendingRetrigger;
    private DoorController pendingDoor;
    private Transform pendingPlayer;

    private DoorController FindStoryDoor()
    {
        foreach (DoorController d in FindObjectsByType<DoorController>(FindObjectsInactive.Include))
            if (d.ambushToTrigger == this) return d;
        foreach (DoorController d in FindObjectsByType<DoorController>(FindObjectsInactive.Include))
            if (!string.IsNullOrEmpty(d.RequiredKey) && d.RequiredKey.ToLower().Contains("guard")) return d;
        return null;
    }

    void Update()
    {
        if (!pendingRetrigger || hasAmbushed || pendingDoor == null) return;
        if (pendingPlayer == null)
        {
            PlayerController p = FindAnyObjectByType<PlayerController>();
            if (p == null) return;
            pendingPlayer = p.transform;
        }
        Vector3 d = pendingDoor.transform.position - pendingPlayer.position;
        if (Mathf.Abs(d.y) < 2.5f && new Vector2(d.x, d.z).magnitude < 3.5f)
        {
            pendingRetrigger = false;
            TriggerAmbush(true);
        }
    }

    /// <summary>The basement event is only marked as done once the player really lies in the basement.</summary>
    private System.Collections.IEnumerator MarkDoneWhenInBasement(Transform player)
    {
        float timeout = Time.time + 40f;
        while (Time.time < timeout)
        {
            if (player != null && basementSpawnPoint != null && Vector3.Distance(player.position, basementSpawnPoint.position) < 3f)
            {
                if (SaveManager.instance != null && !string.IsNullOrEmpty(saveFlag)) SaveManager.instance.SetFlag(saveFlag);
                AmbushLog("player is in the basement - event saved as done");
                yield break;
            }
            yield return new WaitForSeconds(0.25f);
        }
    }

    private string saveFlag;

    [Header("Trigger Settings")]
    [Tooltip("Agar true hai, to player iske qareeb jayega to ye trigger ho jayega (like hallway jumpscare). Agar false hai to sirf Door khulne par chalega!")]
    public bool triggerOnWalk = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnWalk && other.CompareTag("Player"))
        {
            TriggerAmbush();
        }
    }

    // Call this from DoorController's "OnLockedInteract" Event or just walk into the Trigger!
    public void TriggerAmbush(bool forceChase = false)
    {
        Debug.Log("TriggerAmbush called on GameObject: " + gameObject.name + " | Force Chase: " + forceChase);
        
        if (hasAmbushed && !forceChase) 
        {
            Debug.LogWarning("Ambush SKIPPED on " + gameObject.name + " because it was already triggered earlier!");
            return; // Only ambush once, unless forced!
        }
        hasAmbushed = true;
        pendingRetrigger = false;
        // the basement event is saved as done only when the player has really been dropped in the basement
        // (if the game is closed during the jumpscare, it replays next time instead of being lost)
        if (!isBasementTransition && SaveManager.instance != null && !string.IsNullOrEmpty(saveFlag)) SaveManager.instance.SetFlag(saveFlag);

        // Play the sudden scream! (not in the basement scene: there he only says his tease line)
        if (ambushScream != null && !isBasementTransition)
        {
            audioSource.PlayOneShot(ambushScream);
        }

        if (enemy != null && ambushSpawnPoint != null)
        {
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            
            // Fix: NavMeshAgent ko pehle band kar ke position set karni parti hai, warna Unity usay disable kar deta hai aur wo putla ban jata hai!
            if (agent != null) agent.enabled = false;
            
            enemy.transform.position = ambushSpawnPoint.position;
            enemy.EnsureActive();
            
            if (agent != null) 
            {
                agent.enabled = true;
                agent.Warp(ambushSpawnPoint.position);
            }

            // Tell the enemy to Jumpscare or Chase!
            Transform playerTransform = null;
            PlayerStats stats = FindAnyObjectByType<PlayerStats>();

            if (stats != null) 
            {
                playerTransform = stats.transform;
            }

            // Tareeqa 2: Name se dhoondo (Hierarchy mein jo naam hai)
            if (playerTransform == null)
            {
                GameObject p = GameObject.Find("Player");
                if (p != null) playerTransform = p.transform;
            }

            // Tareeqa 3: Tag se dhoondo
            if (playerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }

            if (playerTransform != null)
            {
                // NAYA: Story Transition Check!
                if (isBasementTransition && basementSpawnPoint != null)
                {
                    // WORLD STATE: Mark Nightmare as active so the enemy stays awake while player is in basement!
                    ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
                    if (pm != null) pm.StartNightmarePhase();

                    // Jumpscare karega, uske baad player behosh hoga!
                    StartCoroutine(ExecuteBasementTransition(playerTransform));
                }
                else if (useTeaseInsteadOfChase && !forceChase)
                {
                    enemy.StartTeasing(playerTransform, teaseDuration, teaseLeaveTarget);
                    // WORLD STATE: Mark Nightmare as active even if teasing, so it doesn't go back to sleep!
                    ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
                    if (pm != null) pm.StartNightmarePhase();
                }
                else
                {
                    enemy.ForceAwaken(playerTransform.position);
                    
                    // WORLD STATE: Mark Nightmare as active so it survives death!
                    ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
                    if (pm != null) pm.StartNightmarePhase();
                }
            }
            else
            {
                Debug.LogWarning("Ambush script couldn't find the Player by Script, Name, OR Tag!");
            }
            
            Debug.Log("Cinematic Ambush Started!");
        }
        else
        {
            Debug.LogWarning("Ambush script is missing the Enemy or Spawn Point reference!");
        }
    }

    /// <summary>A camera rotation that faces the target horizontally (no looking down at the floor).</summary>
    private static Quaternion LevelLook(Vector3 from, Vector3 target)
    {
        Vector3 d = target - from; d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(d.normalized) : Quaternion.identity;
    }

    private static float Noise(float x, float y) { return (Mathf.PerlinNoise(x, y) - 0.5f) * 2f; }

    /// <summary>Writes a line to the Console AND to "My project/Logs/AmbushDebug.txt" (so the scene can be checked after a test).</summary>
    public static void AmbushLog(string msg)
    {
        Debug.Log("[Ambush] " + msg);
        try
        {
            string p = System.IO.Path.Combine(Application.dataPath, "../Logs/AmbushDebug.txt");
            System.IO.File.AppendAllText(p, System.DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + "\n");
        }
        catch { }
    }

    private static string AnimInfo(Animator a)
    {
        if (a == null) return "anim=NULL";
        string s = "anim=" + a.name + " ctrl=" + (a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "NONE") + " active=" + a.isActiveAndEnabled;
        if (a.runtimeAnimatorController != null && a.isActiveAndEnabled)
        {
            AnimatorStateInfo st = a.GetCurrentAnimatorStateInfo(0);
            string n = st.IsName("Walking") ? "Walking" : st.IsName("Run") ? "Run" : st.IsName("Standing Yell") ? "Standing Yell(Scream)" :
                       st.IsName("Male Standing Pose") ? "Idle" : st.IsName("Hell Slammer A") ? "Attack" : st.IsName("Grab") ? "Grab" : "?";
            s += " state=" + n;
        }
        return s;
    }

    /// <summary>
    /// BASEMENT JUMPSCARE (rebuilt): the door swings open, the clown SPRINTS out of that room using his NavMeshAgent
    /// (so he is always exactly on the floor, never sunk or floating, and he runs around walls instead of through them),
    /// stops right in front of the player, SCREAMS in his face while he says his tease line and violently shakes him
    /// (short: about 2-3 s, he never just stands there), then grabs him and the drag starts at once.
    /// </summary>
    private System.Collections.IEnumerator ExecuteBasementTransition(Transform playerTransform)
    {
        PlayerController pc = playerTransform.GetComponent<PlayerController>();
        if (pc != null) { pc.isCinematicPlaying = true; pc.SetBodyVisible(false); } // freeze the player and hide his own arms/body: only the clown is on screen

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        Animator anim = enemy.enemyAnimator;
        Camera cam = playerTransform.GetComponentInChildren<Camera>();

        AmbushLog("---- BASEMENT JUMPSCARE START ---- player=" + playerTransform.position + " enemy=" + enemy.transform.position +
                  " agent=" + (agent != null ? ("on=" + agent.enabled + " onMesh=" + (agent.enabled && agent.isOnNavMesh) + " baseOffset=" + agent.baseOffset) : "NULL") +
                  " groundOffset=" + enemy.groundOffset + " cam=" + (cam != null ? cam.name : "NULL") + " " + AnimInfo(anim));

        // the AI brain is switched off for the whole scene; the NavMeshAgent stays ON so it keeps his feet on the floor
        enemy.enabled = false;
        enemy.KeepGrounded();

        // 1. which way: towards the door that started this scene
        Vector3 dir = playerTransform.forward; dir.y = 0f;
        dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
        float stopDist = 1.6f;
        DoorController door = null;
        {
            float bestD = 8f;
            foreach (DoorController dc in FindObjectsByType<DoorController>(FindObjectsSortMode.None))
            {
                if (dc.ambushToTrigger == this) { door = dc; break; }
                float d = Vector3.Distance(dc.transform.position, playerTransform.position);
                if (d < bestD) { bestD = d; door = dc; }
            }
        }
        if (door != null)
        {
            Vector3 c = door.transform.position;
            Collider dcol = door.GetComponentInChildren<Collider>();
            if (dcol != null) c = dcol.bounds.center;
            Vector3 toDoor = c - playerTransform.position; toDoor.y = 0f;
            if (toDoor.magnitude > 0.4f)
            {
                dir = toDoor.normalized;
                stopDist = Mathf.Clamp(toDoor.magnitude, 1.3f, 2.2f);
            }
            door.OpenForCinematic();
            AmbushLog("door=" + door.name + " doorCenter=" + c + " stopDist=" + stopDist);
        }
        else AmbushLog("no door found - he comes from in front of the player");

        // 2. where he stops (ON the NavMesh) and where he starts (deep in that room, reachable by a short path)
        Vector3 stopPos = playerTransform.position + dir * stopDist;
        NavMeshHit nh;
        if (NavMesh.SamplePosition(stopPos, out nh, 2f, NavMesh.AllAreas)) stopPos = nh.position;
        else AmbushLog("WARNING: no NavMesh at the stop point " + stopPos);

        Vector3 start = enemy.transform.position;
        bool startFound = false;
        NavMeshPath path = new NavMeshPath();
        for (float d = 6.5f; d >= 2.5f && !startFound; d -= 1f)
        {
            if (!NavMesh.SamplePosition(stopPos + dir * d, out nh, 1.5f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(nh.position, stopPos, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float len = 0f;
            for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            if (len > d * 1.7f + 1f) continue;   // that point is around a corner / in another room
            start = nh.position; startFound = true;
        }
        AmbushLog("stopPos=" + stopPos + " start=" + start + " startFound=" + startFound);

        // 3. the sprint, driven by the NavMeshAgent (his normal agent settings are put back before the drag)
        float oSpeed = 0f, oAccel = 0f, oAngular = 0f, oStop = 0f; bool oAutoBrake = true;
        if (agent != null) { oSpeed = agent.speed; oAccel = agent.acceleration; oAngular = agent.angularSpeed; oStop = agent.stoppingDistance; oAutoBrake = agent.autoBraking; }
        bool agentOk = agent != null;
        if (agentOk)
        {
            if (!agent.enabled) agent.enabled = true;
            agentOk = agent.Warp(start) && agent.isOnNavMesh;
        }
        if (agentOk)
        {
            agent.isStopped = false;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.speed = 6.5f;
            agent.acceleration = 60f;
            agent.angularSpeed = 1080f;
            agent.stoppingDistance = 0.05f;
            agent.autoBraking = true;
            agent.SetDestination(stopPos);
        }
        else
        {
            AmbushLog("WARNING: agent could not be placed on the NavMesh - using a direct move");
            if (agent != null) agent.enabled = false;
            enemy.transform.position = start;
        }
        Vector3 face0 = stopPos - start; face0.y = 0f;
        if (face0.sqrMagnitude > 0.01f) enemy.transform.rotation = Quaternion.LookRotation(face0.normalized);
        enemy.KeepGrounded();

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.speed = 1f;
            anim.ResetTrigger("Tease"); anim.ResetTrigger("Attack");
            anim.SetFloat("Speed", 6f);
            anim.CrossFadeInFixedTime("Run", 0.1f);
        }

        Vector3 originalLocalPos = cam != null ? cam.transform.localPosition : Vector3.zero;
        Quaternion originalLocalRot = cam != null ? cam.transform.localRotation : Quaternion.identity;
        float startFOV = cam != null ? cam.fieldOfView : 60f;

        float t = 0f;
        float runMax = 2.6f;
        Vector3 directFrom = enemy.transform.position;
        while (t < runMax)
        {
            t += Time.deltaTime;
            if (agentOk)
            {
                if (!agent.pathPending && agent.remainingDistance <= 0.2f) break;
            }
            else
            {
                float k = Mathf.Clamp01(t / 1.1f);
                enemy.transform.position = Vector3.Lerp(directFrom, stopPos, k);
                if (k >= 1f) break;
            }
            enemy.KeepGrounded();
            if (anim != null && anim.runtimeAnimatorController != null) anim.SetFloat("Speed", 6f);
            if (cam != null)
            {
                // the player only turns his head towards the noise and looks STRAIGHT ahead (level): the camera is not
                // pulled down after his hunched head any more - he runs INTO the view
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, LevelLook(cam.transform.position, enemy.transform.position), Time.deltaTime * 6f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, startFOV + 4f, Time.deltaTime * 4f);
            }
            yield return null;
        }
        AmbushLog("sprint done after " + t.ToString("0.00") + "s, enemy=" + enemy.transform.position + " " + AnimInfo(anim));

        // 4. LUNGE: he does not stop at the doorway - he crashes straight into the player and grabs him.
        //    The agent has already put his root at the correct floor height, so from here he only moves sideways (XZ).
        if (agent != null)
        {
            agent.speed = oSpeed; agent.acceleration = oAccel; agent.angularSpeed = oAngular;
            agent.stoppingDistance = oStop; agent.autoBraking = oAutoBrake; agent.updateRotation = true;
            agent.enabled = false;   // the drag switches it back on at the end
        }
        float rootY = enemy.transform.position.y;
        Vector3 toEnemy = enemy.transform.position - playerTransform.position; toEnemy.y = 0f;
        Vector3 dirE = toEnemy.sqrMagnitude > 0.001f ? toEnemy.normalized : playerTransform.forward;
        Vector3 grabPos = playerTransform.position + dirE * 1.05f;   // right in the player's face, but his whole face fits on screen
        grabPos.y = rootY;
        Vector3 lungeFrom = enemy.transform.position;
        enemy.transform.rotation = Quaternion.LookRotation(-dirE);
        float lungeTime = Mathf.Clamp(Vector3.Distance(lungeFrom, grabPos) / 9f, 0.18f, 0.45f);
        t = 0f;
        while (t < lungeTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / lungeTime); k = k * k;          // accelerates into the player
            enemy.transform.position = Vector3.Lerp(lungeFrom, grabPos, k);
            enemy.KeepGrounded();
            if (anim != null && anim.runtimeAnimatorController != null) anim.SetFloat("Speed", 6f);
            if (cam != null)
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, LevelLook(cam.transform.position, enemy.transform.position), Time.deltaTime * 10f);
            yield return null;
        }
        enemy.transform.position = grabPos;

        // 5. the GRAB: he holds the player by the shoulders, SCREAMS in his face (tease line) and shakes him like in a movie
        if (anim != null && anim.runtimeAnimatorController != null) anim.SetFloat("Speed", 0f);
        float hold = Mathf.Clamp(enemy.TeaseDialogueLength + 0.3f, 2.6f, 3.4f);
        enemy.StartTeasing(playerTransform, hold, null, true);   // scream animation + his tease line (the ONLY place it is used)
        enemy.KeepGrounded();
        AmbushLog("GRAB at " + grabPos + " (lunge " + lungeTime.ToString("0.00") + "s), hold " + hold.ToString("0.0") + "s, " + AnimInfo(anim));

        if (cam != null)
        {
            Vector3 camWorldStart = cam.transform.position;
            Quaternion camWorldRotStart = cam.transform.rotation;
            float twoPi = Mathf.PI * 2f;
            Vector3 lookPt = enemy.transform.position + Vector3.up * 0.5f;
            Vector3 lookVel = Vector3.zero;
            t = 0f;
            while (t < hold)
            {
                t += Time.deltaTime;
                enemy.KeepGrounded();

                // shaking comes in 3 heavy waves (held by the shoulders): each wave starts with a hard jolt and fades
                float cycle = t % 1.1f;
                float env = Mathf.Clamp01(t / 0.06f) * (0.35f + 0.65f * Mathf.Exp(-cycle * 2.2f));
                float jolt = Mathf.Exp(-cycle * 10f);                       // the hard yank at the start of each wave
                float impact = Mathf.Exp(-t * 8f);                          // the hit when he slams into the player
                float back = Mathf.Sin(t * twoPi * 3.2f);                   // pulled to his face and pushed back, smooth

                // he stays planted (no jitter of his body); only a small recoil from the impact
                enemy.transform.position = grabPos - dirE * (impact * 0.12f);
                enemy.KeepGrounded();

                // the camera looks at a SMOOTHED point on his face, so his head animation can't make the view jitter
                // the camera stays LEVEL at his face height (root sits 1 m up, his face ~1.55 m): when the scream makes him
                // hunch down and rear up again, he rises INTO the view instead of the camera diving to the floor after his skull
                // the grab knocks the player down to his knees: the camera drops, so the clown TOWERS over him
                float kneel = 0.38f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.3f));

                // look at his face (smoothed so his head animation can't make the view jitter) but NEVER downwards:
                // the player always looks UP at him
                Transform hb = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
                Vector3 head = hb != null ? hb.position : enemy.transform.position + Vector3.up * 0.5f;
                float minY = camWorldStart.y - kneel + 0.08f;
                if (head.y < minY) head.y = minY;
                lookPt = Vector3.SmoothDamp(lookPt, head, ref lookVel, 0.15f);

                Vector3 camPos = camWorldStart - Vector3.up * kneel
                               - dirE * (impact * 0.3f)
                               + dirE * (0.08f + back * 0.06f * env + jolt * 0.07f)
                               + new Vector3(Noise(t * 9f, 1f), Noise(t * 10f, 7f) * 0.6f, Noise(t * 8f, 13f)) * 0.025f * env;
                cam.transform.position = camPos;

                float pitch = Mathf.Sin(t * twoPi * 3.2f + 0.7f) * 4f * env - jolt * 6f - impact * 10f;
                float roll  = Mathf.Sin(t * twoPi * 1.6f) * 6f * env + Noise(t * 7f, 9f) * 2f * env;
                float yaw   = Noise(t * 8f, 5f) * 2.5f * env;
                Quaternion look = Quaternion.LookRotation((lookPt - camPos).normalized);
                cam.transform.rotation = Quaternion.Slerp(camWorldRotStart, look, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.18f))) * Quaternion.Euler(pitch, yaw, roll);
                cam.fieldOfView = Mathf.Lerp(startFOV + 8f, startFOV - 10f, Mathf.Clamp01(t / 0.25f)) + jolt * 4f;

                // wait for the end of the frame: the Animator has moved his head by then
                yield return new WaitForEndOfFrame();
            }
            enemy.transform.position = grabPos;
            cam.fieldOfView = startFOV;
            // the camera is NOT snapped back to standing here (that was a one-frame jump): the drag continues
            // smoothly from exactly where it is now; the wake-up in the basement resets it
            if (pc == null) { cam.transform.localPosition = originalLocalPos; cam.transform.localRotation = originalLocalRot; }
        }
        else
        {
            yield return new WaitForSeconds(hold);
        }
        AmbushLog("grab done -> drag. enemy=" + enemy.transform.position + " " + AnimInfo(anim));

        if (pc != null) pc.isCinematicPlaying = false; // Restore normal control logic

        // 6. Player faints and is dragged to the basement!
        if (pc != null)
        {
            pc.TriggerDragToBasementCinematic(basementSpawnPoint, enemy.gameObject);
            StartCoroutine(MarkDoneWhenInBasement(playerTransform));
        }
    }
}
