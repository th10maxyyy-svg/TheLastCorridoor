using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState { Dormant, Patrol, Chase, Search, InvestigateSound, Tease, LeaveTease, VanishAfterKill, Stunned, CinematicEscape }

    [Header("State")]
    [SerializeField] private AIState currentState = AIState.Dormant; // Starts asleep!

    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 15f;
    [SerializeField] private float fovAngle = 90f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Speed Settings")]
    [SerializeField] private float patrolSpeed = 2.0f;
    [SerializeField] private float chaseSpeed = 3.5f;

    [Header("Search Settings")]
    [SerializeField] private float searchDuration = 5f;
    [SerializeField] private float searchRadius = 5f;

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float damageAmount = 25f;
    [SerializeField] private float attackRate = 1.5f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource enemyAudioSource;
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField] private AudioClip chaseMusic;
    [SerializeField] private AudioClip ambientSound;
    [SerializeField] private AudioClip[] teaseVoiceLines; // Voice lines for teasing the player
    [Tooltip("Clown ke random bolne ke sounds (laughs, grunts) isme dalein")]
    [SerializeField] private AudioClip[] patrolVoiceLines; 
    [SerializeField] private float minVoiceDelay = 5f;
    [SerializeField] private float maxVoiceDelay = 15f;
    [SerializeField] private float stepInterval = 0.6f;

    [Header("Animation Settings")]
    [Tooltip("Isme churail (witch) ka 3D model drag karein jis par Animator laga ho")]
    public Animator enemyAnimator;
    [Tooltip("Agar attack ke waqt clown terha (sideways) ho jaye, to yahan 90 ya -90 set karein")]
    public float attackRotationOffset = 90f;
    [Tooltip("Agar enemy hawa mein hai to isay -1.2 ya -1.3 karein taake paon zameen par aa jayen")]
    public float groundOffset = -1.1f;

    [Header("Attack Animation Timing")]
    [Tooltip("Attack clip (Hell Slammer A) shuru hone ke itne seconds baad damage lagta hai. Isay clip ke 'slam' wale moment ke mutabiq set karein.")]
    public float attackHitDelay = 1.4f;
    [Tooltip("Attack animation ki lambai (seconds). Agla attack isse pehle nahi hota, taake animation beech mein restart na ho.")]
    public float attackAnimLength = 2.4f;

    private float stepTimer = 0f;
    private float teaseTimer = 0f; // Timer for how long the enemy teases
    private float nextVoiceTime = 0f; // NEW: Timer for random patrol voices
    private Transform teaseTarget; // Who to look at while teasing
    private Transform teaseLeaveTarget; // Where to go after teasing
    private NavMeshAgent agent;
    private Transform playerTransform;
    private PlayerStats playerStats;

    private Vector3 lastKnownPosition;
    private float searchTimer;
    private float nextAttackTime;
    private Vector3 patrolTarget;
    private bool hasInitializedPatrol = false;
    private float stuckTimer = 0f;
    private Vector3 lastStuckPosition;
    private float ignorePlayerTimer = 0f;
    private float leaveStuckTimer = 0f;
    private bool hasSetLeaveDestination = false;

    // Breadcrumb system for safe teleports
    private System.Collections.Generic.List<Vector3> playerBreadcrumbs = new System.Collections.Generic.List<Vector3>();
    private float breadcrumbTimer = 0f;
    
    private float navMeshCheckCooldown = 0f;

    [Header("Chase Settings")]
    [Tooltip("Player nazar se ojhal ho jaye to kitne seconds baad enemy chase chhor kar dhoondna shuru kare")]
    [SerializeField] private float loseSightTime = 3f;
    [Tooltip("Attack sirf tab jab player itni height ke farq ke andar ho (warna farsh/chhat ke aar paar maarta tha)")]
    [SerializeField] private float maxAttackHeightDifference = 1.8f;

    [Header("Debug")]
    [Tooltip("Screen par enemy ki debug info dikhaye (sirf testing ke liye)")]
    [SerializeField] private bool showDebugOverlay = false;

    private PlayerController playerController;
    private float lostSightTimer = 0f;
    private float stunEndTime = 0f;
    private float stunOriginalXRotation = 0f;
    private bool externallyActivated = false;
    private float playerMissingWarnTime = 0f;

    /// <summary>Inspector values, so the DDA system adjusts around them instead of hard-coded numbers.</summary>
    public float BaseChaseSpeed { get; private set; }
    public float BaseDetectionRange { get; private set; }
    public float BaseSearchDuration { get; private set; }
    public float BaseSearchRadius { get; private set; }

    // ---------------- ADAPTIVE AI (set by PlayerBehaviorTracker, see FYP document Section 7.4) ----------------
    /// <summary>Current aggression level 1 (gentle) to 5 (relentless). 3 = the Inspector values.</summary>
    public int AggressionLevel { get; private set; } = 3;
    private bool hasFavouriteSpot = false;
    private Vector3 favouriteSpot;
    private int searchPhase = 0;          // 0 = go to last known position, 1 = check favourite hiding spot, 2 = sweep around
    private float spotLingerTimer = 0f;
    private bool favouriteChecked = false;

    public AIState State => currentState;

    [Header("Horror Behaviour")]
    [Tooltip("Patrol ke doran kabhi kabhi ruk kar player ki taraf ghoorta aur hansta hai")]
    [SerializeField] private bool stalkPauses = true;
    [Tooltip("Chase mein player se itna faasla rakhta hai (warna model camera ke andar ghus jata tha)")]
    [SerializeField] private float chaseStandOffDistance = 1.1f;

    private float nextStalkPauseTime = 0f;
    private float stalkPauseUntil = 0f;
    private float nextSpottedLineTime = 0f;
    private float chaseYellUntil = 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (BaseChaseSpeed <= 0f) // (may already be set if the adaptive AI configured us while inactive)
        {
            BaseChaseSpeed = chaseSpeed;
            BaseDetectionRange = detectionRange;
            BaseSearchDuration = searchDuration;
            BaseSearchRadius = searchRadius;
        }

        LoadDefaultAudio();
        if (GetComponent<EnemyHorrorFX>() == null) gameObject.AddComponent<EnemyHorrorFX>();

        // Failsafe: Agar user enemy ko disable karna bhool jaye, to game shuru hote hi ye khud chupp jayega!
        // (Lekin agar kisi script ne isay jagane ke liye EnsureActive() call kiya hai to wapas band na ho)
        if (currentState == AIState.Dormant && !externallyActivated)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Scene mein enemy ki cheekh / hansi wali lists khali thin (is liye woh bilkul chup tha).
    /// Agar Inspector mein kuch nahi dala gaya to Resources/HorrorAudio se khud bhar lo.
    /// </summary>
    private void LoadDefaultAudio()
    {
        if (teaseVoiceLines == null || teaseVoiceLines.Length == 0)
        {
            teaseVoiceLines = LoadClips("HorrorAudio/Peek_a_boo_I_finally_found", "HorrorAudio/sound_should_be_like_heath_ledg");
        }
        if (patrolVoiceLines == null || patrolVoiceLines.Length == 0)
        {
            patrolVoiceLines = LoadClips("HorrorAudio/oliper18-scary-laugh-377526", "HorrorAudio/joker-2008-laugh");
        }
        if (enemyAudioSource != null)
        {
            // 3D voice so the player can tell WHERE he is in the dark
            enemyAudioSource.spatialBlend = Mathf.Max(enemyAudioSource.spatialBlend, 0.85f);
            enemyAudioSource.rolloffMode = AudioRolloffMode.Linear;
            enemyAudioSource.minDistance = 2f;
            enemyAudioSource.maxDistance = Mathf.Max(enemyAudioSource.maxDistance, 35f);
        }
    }

    private static AudioClip[] LoadClips(params string[] paths)
    {
        System.Collections.Generic.List<AudioClip> clips = new System.Collections.Generic.List<AudioClip>();
        foreach (string p in paths)
        {
            AudioClip c = Resources.Load<AudioClip>(p);
            if (c != null) clips.Add(c);
        }
        return clips.ToArray();
    }

    /// <summary>He spotted the player: start the chase with a "Peek-a-boo... I finally found you!" (not every few seconds).</summary>
    private void BeginChase()
    {
        currentState = AIState.Chase;
        stalkPauseUntil = 0f;
        if (Time.time >= nextSpottedLineTime && teaseVoiceLines != null && teaseVoiceLines.Length > 0 && enemyAudioSource != null)
        {
            enemyAudioSource.PlayOneShot(teaseVoiceLines[0]);
            nextSpottedLineTime = Time.time + 20f;
            if (enemyAnimator != null) enemyAnimator.SetTrigger("Tease");
            chaseYellUntil = Time.time + 1.3f; // stops for a moment and screams before running at the player
        }
    }

    /// <summary>Use this instead of gameObject.SetActive(true) to wake the enemy up.</summary>
    public void EnsureActive()
    {
        externallyActivated = true;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    /// <summary>After a death-respawn: back on the hunt, but gives the player a few seconds before it can spot them.</summary>
    public void ResumeHuntAfterRespawn(float graceSeconds)
    {
        EnsureActive();
        currentState = AIState.Patrol;
        ignorePlayerTimer = graceSeconds;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = patrolSpeed;
        }
        hasInitializedPatrol = false; // picks a fresh patrol target once on the NavMesh
    }

    void OnDisable()
    {
        // If disabled mid-stun the coroutine dies - never leave him frozen/lying down
        if (currentState == AIState.Stunned)
        {
            StandUpFromStun();
            currentState = AIState.Patrol;
        }
        if (agent != null) agent.updateRotation = true;
    }

    void Start()
    {
        // Tareeqa 1: Script se dhoondo
        PlayerStats stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null) playerTransform = stats.transform;

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
            playerStats = playerTransform.GetComponent<PlayerStats>();
            playerController = playerTransform.GetComponent<PlayerController>();
        }

        if (agent != null) agent.speed = patrolSpeed;
        nextVoiceTime = Time.time + Random.Range(minVoiceDelay, maxVoiceDelay);
    }

    void Update()
    {
        if (agent == null) return;

        if (ignorePlayerTimer > 0)
        {
            ignorePlayerTimer -= Time.deltaTime;
        }

        // Record breadcrumbs every 0.5 seconds
        if (playerTransform != null)
        {
            breadcrumbTimer += Time.deltaTime;
            if (breadcrumbTimer >= 0.5f)
            {
                playerBreadcrumbs.Add(playerTransform.position);
                if (playerBreadcrumbs.Count > 20) // Keep last 10 seconds of positions
                {
                    playerBreadcrumbs.RemoveAt(0);
                }
                breadcrumbTimer = 0f;
            }
        }

        // Safe Check: Agar agent NavMesh par nahi hai to AI run na karein
        if (!agent.isOnNavMesh)
        {
            navMeshCheckCooldown -= Time.deltaTime;
            
            if (navMeshCheckCooldown <= 0f)
            {
                navMeshCheckCooldown = 1f; // Check once per second, NOT every frame!
                
                // Try to snap/warp the enemy to the nearest NavMesh point
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 10.0f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    Debug.Log("Enemy ko NavMesh par wapas snap kar diya gaya hai.");
                }
                else
                {
                    // Agar floor delete ho chuka hai aur enemy bohat pichay reh gaya hai to usay aagay teleport kar dein
                    if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) > 30f)
                    {
                        Debug.Log("Enemy bohat pichay reh gaya tha, teleporting safely using Breadcrumbs.");
                        
                        // Safe Teleport: Player jahan 5-10 second pehle tha, wahan teleport karo!
                        if (playerBreadcrumbs.Count > 5)
                        {
                            Vector3 safePos = playerBreadcrumbs[0]; // Oldest position
                            NavMeshHit tp_hit;
                            if (NavMesh.SamplePosition(safePos, out tp_hit, 5f, NavMesh.AllAreas))
                            {
                                agent.Warp(tp_hit.position);
                                currentState = AIState.Search;
                                ResetSearchPlan();
                                Debug.Log("Enemy safely teleported behind the player.");
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Enemy is NOT on the NavMesh and can't find a close point!");
                    }
                }
            }
            return; // Jab tak NavMesh na mile, aagay ka code na chalaye
        }

        // Ensure agent is not stopped (unless dormant, teasing or stunned)
        if (agent.isStopped && currentState != AIState.Dormant && currentState != AIState.Tease && currentState != AIState.LeaveTease && currentState != AIState.Stunned && currentState != AIState.VanishAfterKill && currentState != AIState.CinematicEscape && (playerStats == null || !playerStats.IsDead()))
        {
            agent.isStopped = false;
        }

        // Attack ke waqt rotation band hoti hai - chase ke ilawa har state mein NavMesh ko wapas rotation do (warna enemy ulta chalta tha)
        if (currentState != AIState.Chase && currentState != AIState.Stunned && !agent.updateRotation)
        {
            agent.updateRotation = true;
        }
        if (currentState != AIState.Chase && agent.stoppingDistance != 0f)
        {
            agent.stoppingDistance = 0f; // stand-off distance is only for the chase
        }

        // Animator ki speed update karein (taake walk/run automatically ho)
        if (enemyAnimator != null)
        {
            float animSpeed = agent.velocity.magnitude;
            // Ending cinematic: while he stands screaming, leftover agent velocity must not pull him out of the yell pose
            if (currentState == AIState.CinematicEscape && (cinematicHolding || (hasFlinched && flinchTimer > 0f))) animSpeed = 0f;
            enemyAnimator.SetFloat("Speed", animSpeed);
        }

        // Start patrolling once the agent is on the NavMesh
        if (!hasInitializedPatrol)
        {
            if (currentState == AIState.Patrol) GetNewPatrolTarget();
            hasInitializedPatrol = true;
        }

        // Handle game over / player death safely
        if (playerTransform == null)
        {
            if (Time.time >= playerMissingWarnTime)
            {
                Debug.LogWarning("EnemyAI: Player nahi mila! Enemy isliye nahi chal raha.");
                playerMissingWarnTime = Time.time + 5f; // har frame console spam nahi
            }
            return;
        }

        if (playerStats != null && playerStats.IsDead())
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            return;
        }

        switch (currentState)
        {
            case AIState.Dormant:
                // Do absolutely nothing until awakened
                if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
                break;
            case AIState.Patrol:
                PatrolBehavior();
                break;
            case AIState.Chase:
                ChaseBehavior();
                break;
            case AIState.Search:
                SearchBehavior();
                break;
            case AIState.InvestigateSound:
                InvestigateSoundBehavior();
                break;
            case AIState.Tease:
                TeaseBehavior();
                break;
            case AIState.LeaveTease:
                LeaveTeaseBehavior();
                break;
            case AIState.VanishAfterKill:
                // Do nothing, just stand still
                break;
            case AIState.Stunned:
                // Frozen by coroutine, do nothing
                break;
            case AIState.CinematicEscape:
                CinematicEscapeBehavior();
                break;
        }
    }

    // --- PHASE 4 ESCAPE CINEMATIC LOGIC ---
    private Transform cinematicRunTarget;
    private Transform cinematicRetreatTarget;
    private string cinematicAnimationName;
    private bool hasFlinched = false;
    private float flinchTimer = 0f;

    [Header("Escape Cinematic Tuning")]
    [Tooltip("How close (metres) the enemy gets to the run target before he stops and screams")]
    public float cinematicStopDistance = 2f;
    [Tooltip("When true the ending cinematic plays all enemy voices itself (timed to the animation); he makes no voice of his own")]
    public bool silentCinematicVoice = false;
    [Tooltip("How long he screams at the gate before walking back into the darkness")]
    public float cinematicFlinchDuration = 3f;
    [Tooltip("Run speed multiplier (x chaseSpeed) during the escape cinematic")]
    public float cinematicRunSpeedMultiplier = 1.5f;
    [Tooltip("If > 0 he RUNS back into the darkness at chaseSpeed x this value instead of a slow defeated walk")]
    public float cinematicRetreatSpeedMultiplier = 0f;
    /// <summary>True once the enemy has reached the gate and is screaming/retreating.</summary>
    public bool CinematicHasArrived => hasFlinched;

    public void PlayCinematicEscape(Transform runTarget, Transform retreatTarget, string animationName)
    {
        ResetAnimTriggers();
        // If he was lying on the floor from the taser, stand him up first (otherwise he "runs" frozen and sideways)
        if (currentState == AIState.Stunned) StandUpFromStun();
        StopAllCoroutines(); // no stun / vanish routine may interfere with the ending

        // Chase music and the ambient loop must not play under the ending
        if (enemyAudioSource != null && enemyAudioSource.clip != null)
        {
            enemyAudioSource.Stop();
            enemyAudioSource.clip = null;
        }

        currentState = AIState.CinematicEscape;
        cinematicDestinationSet = false;
        cinematicHolding = false;
        cinematicRunTarget = runTarget;
        cinematicRetreatTarget = retreatTarget;
        cinematicAnimationName = animationName;
        hasFlinched = false;
        
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed * cinematicRunSpeedMultiplier; // Fast aggressive run towards the door!
            agent.updateRotation = true;
            cinematicDestinationSet = agent.SetDestination(cinematicRunTarget.position);
        }
    }

    private bool cinematicDestinationSet = false;
    private bool cinematicHolding = false;
    private Vector3 cinematicLookPoint;

    /// <summary>
    /// Ending cinematic, part 1: he stands still in the dark, stares at the player and (optionally) screams.
    /// Call PlayCinematicEscape afterwards to make him charge.
    /// </summary>
    public void HoldForCinematic(Vector3 lookPoint, string animTrigger, bool scream)
    {
        ResetAnimTriggers();
        if (currentState == AIState.Stunned) StandUpFromStun();
        StopAllCoroutines();

        if (enemyAudioSource != null && enemyAudioSource.clip != null)
        {
            enemyAudioSource.Stop();
            enemyAudioSource.clip = null;
        }

        currentState = AIState.CinematicEscape;
        cinematicHolding = true;
        hasFlinched = false;
        cinematicLookPoint = lookPoint;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (enemyAnimator != null && !string.IsNullOrEmpty(animTrigger)) enemyAnimator.SetTrigger(animTrigger);
        if (scream) PlayScream();
    }

    /// <summary>Plays one of his tease/scream voice lines.</summary>
    public void PlayScream()
    {
        if (enemyAudioSource != null && teaseVoiceLines != null && teaseVoiceLines.Length > 0)
        {
            AudioClip clip = teaseVoiceLines[Random.Range(0, teaseVoiceLines.Length)];
            if (clip != null) enemyAudioSource.PlayOneShot(clip);
        }
    }

    private void StandUpFromStun()
    {
        if (enemyAnimator != null) enemyAnimator.speed = 1f;
        transform.localEulerAngles = new Vector3(stunOriginalXRotation, transform.localEulerAngles.y, transform.localEulerAngles.z);
        if (agent != null) agent.updateRotation = true;
    }

    private void CinematicEscapeBehavior()
    {
        // 0. Standing in the dark, staring at the player
        if (cinematicHolding)
        {
            Vector3 dir = cinematicLookPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 4f);
            }
            return;
        }

        if (cinematicRunTarget == null) return;

        // 1. Running towards the light
        if (!hasFlinched)
        {
            // BUG FIX: agar shuru mein agent NavMesh par nahi tha to destination kabhi set nahi hoti thi
            // aur "remainingDistance = 0" ki wajah se wo spawn par hi khara ho kar cheekhne lagta tha.
            if (!cinematicDestinationSet || (!agent.hasPath && !agent.pathPending))
            {
                agent.isStopped = false;
                agent.speed = chaseSpeed * cinematicRunSpeedMultiplier;
                cinematicDestinationSet = agent.SetDestination(cinematicRunTarget.position);
            }

            HandleFootsteps(0.4f); // SOUND SYNC: one footstep per stride of the Run animation (0.63 s cycle, 2 steps)

            Vector3 flat = cinematicRunTarget.position - transform.position;
            flat.y = 0f;
            float arriveDistance = Mathf.Max(cinematicStopDistance, agent.stoppingDistance + 0.1f);
            bool reached = flat.magnitude < arriveDistance ||
                           (cinematicDestinationSet && agent.hasPath && !agent.pathPending && agent.remainingDistance < arriveDistance);

            if (reached)
            {
                // Reached the door, trigger the flinch/scream!
                hasFlinched = true;
                flinchTimer = cinematicFlinchDuration; // Scream at the gate
                
                if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }
                
                if (enemyAnimator != null && !string.IsNullOrEmpty(cinematicAnimationName))
                {
                    enemyAnimator.SetTrigger(cinematicAnimationName); // Trigger custom user animation
                }
                
                if (!silentCinematicVoice && enemyAudioSource != null && teaseVoiceLines != null && teaseVoiceLines.Length > 0)
                {
                    enemyAudioSource.PlayOneShot(teaseVoiceLines[0]); // Scream sound
                }
            }
        }
        // 2. Screaming at the light
        else if (flinchTimer > 0)
        {
            flinchTimer -= Time.deltaTime;
            if (flinchTimer <= 0)
            {
                // Time to retreat back into the darkness
                if (agent.isActiveAndEnabled && agent.isOnNavMesh && cinematicRetreatTarget != null)
                {
                    agent.isStopped = false;
                    agent.speed = cinematicRetreatSpeedMultiplier > 0f ? chaseSpeed * cinematicRetreatSpeedMultiplier : patrolSpeed; // walk back, or flee in panic
                    agent.SetDestination(cinematicRetreatTarget.position);
                }
            }
        }
        // 3. Retreating
        else
        {
            HandleFootsteps(cinematicRetreatSpeedMultiplier > 0f ? 0.4f : 0.6f); // Slow footsteps (or panicked sprint)
        }
    }

    void LateUpdate()
    {
        if (enemyAnimator != null)
        {
            // The attack clip faces sideways, so the model is turned by 'attackRotationOffset' while it plays.
            // ANIMATION FIX: the turn is now blended together with the Animator transition. Before, it snapped
            // 90 degrees the instant the state changed, which made him pop sideways at the start and end of every attack.
            AnimatorStateInfo cur = enemyAnimator.GetCurrentAnimatorStateInfo(0);
            float weight = cur.IsName("Hell Slammer A") ? 1f : 0f;
            if (enemyAnimator.IsInTransition(0))
            {
                AnimatorStateInfo next = enemyAnimator.GetNextAnimatorStateInfo(0);
                float k = Mathf.Clamp01(enemyAnimator.GetAnimatorTransitionInfo(0).normalizedTime);
                weight = Mathf.Lerp(weight, next.IsName("Hell Slammer A") ? 1f : 0f, k);
            }
            enemyAnimator.transform.localRotation = Quaternion.Euler(0f, attackRotationOffset * weight, 0f);
            
            // Paon ko hawa mein janay se rokne ke liye position bhi lock rakhein
            enemyAnimator.transform.localPosition = new Vector3(0, groundOffset, 0);
        }
    }

    // --- NEW: HEARING SYSTEM (GRANNY STYLE) ---
    private Vector3 soundInvestigationPoint;
    private float soundInvestigateTimer;
    private float currentInvestigateDuration = 3f;

    /// <summary>
    /// Sounds may only pull him away from normal roaming. While asleep, stunned, teasing, chasing,
    /// vanishing or in the ending cinematic a noise must NOT change his state (that used to break the stun & cinematics).
    /// </summary>
    private bool CanBeDistractedByNoise()
    {
        return currentState == AIState.Patrol || currentState == AIState.Search || currentState == AIState.InvestigateSound;
    }

    public void HearNoise(Vector3 noiseLocation)
    {
        if (!CanBeDistractedByNoise()) return;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        // Don't hear it if it's too far away
        if (Vector3.Distance(transform.position, noiseLocation) > detectionRange * 2f) return;

        Debug.Log("EnemyAI: Heard a noise! Investigating...");
        soundInvestigationPoint = noiseLocation;
        currentState = AIState.InvestigateSound;
        currentInvestigateDuration = 3f; // Default search time
        soundInvestigateTimer = 0f;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(noiseLocation, out hit, 5f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            agent.speed = chaseSpeed * 0.8f; // Walk fast to the sound, but not full sprint
        }
    }

    public void ForceInvestigate(Vector3 noiseLocation, float searchTime = 15f)
    {
        if (!CanBeDistractedByNoise()) return;

        Debug.Log("EnemyAI: FORCED to investigate loud noise!");
        soundInvestigationPoint = noiseLocation;
        currentState = AIState.InvestigateSound;
        currentInvestigateDuration = searchTime; // Custom search time
        soundInvestigateTimer = 0f;

        if (agent == null) agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(noiseLocation, out hit, 5f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                agent.speed = chaseSpeed * 0.9f; 
            }
        }
    }

    private void InvestigateSoundBehavior()
    {
        HandleFootsteps();
        
        if (CanSeePlayer())
        {
            BeginChase();
            return;
        }

        // Reached the sound location
        if (!agent.pathPending && (agent.remainingDistance < 1f || agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid || !agent.hasPath))
        {
            soundInvestigateTimer += Time.deltaTime;
            
            // Look around for the specified duration
            if (soundInvestigateTimer > currentInvestigateDuration)
            {
                soundInvestigateTimer = 0f;
                currentState = AIState.Patrol;
                GetNewPatrolTarget();
            }
        }
    }

    public void TakeStun(float duration)
    {
        if (currentState == AIState.Dormant || currentState == AIState.VanishAfterKill || currentState == AIState.CinematicEscape) return;
        if (!gameObject.activeInHierarchy) return;

        // Already stunned: just extend the stun instead of starting a second, conflicting coroutine
        if (currentState == AIState.Stunned)
        {
            stunEndTime = Mathf.Max(stunEndTime, Time.time + duration);
            return;
        }

        stunEndTime = Time.time + duration;
        StartCoroutine(StunRoutine());
    }

    private System.Collections.IEnumerator StunRoutine()
    {
        AIState previousState = currentState;
        currentState = AIState.Stunned;
        ResetAnimTriggers(); // a queued Attack/Tease must not fire the moment he stands up again

        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        agent.updateRotation = false; // NavMesh must not fight the "lying down" rotation

        if (enemyAnimator != null)
        {
            enemyAnimator.speed = 0f; // Freeze animation
        }

        // Dushman ko zameen par lita dein (Rotate 90 degrees backward)
        stunOriginalXRotation = transform.localEulerAngles.x;
        transform.localEulerAngles = new Vector3(90f, transform.localEulerAngles.y, transform.localEulerAngles.z);

        while (Time.time < stunEndTime) yield return null;

        // Dushman ko wapas khara karein
        transform.localEulerAngles = new Vector3(stunOriginalXRotation, transform.localEulerAngles.y, transform.localEulerAngles.z);
        agent.updateRotation = true;
        
        if (enemyAnimator != null)
        {
            enemyAnimator.speed = 1f; // Unfreeze animation
        }
        
        // Something else (e.g. the ending cinematic) took over while he was stunned - don't overwrite that state
        if (currentState != AIState.Stunned) yield break;

        // After stun, the enemy is angry and immediately chases the player if possible
        if (playerTransform != null)
        {
            currentState = AIState.Chase;
        }
        else
        {
            currentState = previousState;
        }
        
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    private void PatrolBehavior()
    {
        agent.speed = patrolSpeed;
        HandleFootsteps();

        // Clown ki random darawni aawazein (Random Voices)
        if (patrolVoiceLines != null && patrolVoiceLines.Length > 0 && Time.time >= nextVoiceTime)
        {
            AudioClip voice = patrolVoiceLines[Random.Range(0, patrolVoiceLines.Length)];
            if (enemyAudioSource != null && voice != null)
            {
                enemyAudioSource.PlayOneShot(voice); // Play without stopping ambient sound
            }
            // Naya time set karein agli aawaz ke liye
            nextVoiceTime = Time.time + Random.Range(minVoiceDelay, maxVoiceDelay);
        }

        ResetAudioToAmbient();

        if (CanSeePlayer())
        {
            Debug.Log("EnemyAI: Player nazar aa gaya, ab Chase karega!");
            BeginChase();
            return;
        }

        // --- STALKING PAUSE: kabhi kabhi ruk kar player ki disha mein ghoorta hai aur hansta hai ---
        if (StalkPause()) return;

        // --- GRANNY-LIKE WANDER ---
        // Enemy ab har waqt player ke peeche nahi bhagega. Ye randomly patrol karega.

        if (Vector3.Distance(transform.position, lastStuckPosition) < 0.1f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 2f)
            {
                Debug.LogWarning("EnemyAI: Enemy deewar me phans gaya tha (stuck), naya rasta dhoond raha hai.");
                GetNewPatrolTarget();
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
            lastStuckPosition = transform.position;
        }

        // Agar rasta khatam ho jaye, toot jaye, ya pahunch jaye, to naya rasta dhundo
        if (!agent.pathPending && (agent.remainingDistance < 0.5f || agent.pathStatus == NavMeshPathStatus.PathInvalid || !agent.hasPath))
        {
            GetNewPatrolTarget();
        }
    }

    /// <summary>Returns true while he is standing still, staring towards where the player is.</summary>
    private bool StalkPause()
    {
        if (!stalkPauses || playerTransform == null) return false;

        if (Time.time < stalkPauseUntil)
        {
            agent.isStopped = true;
            Vector3 dir = playerTransform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 2f);
            return true;
        }

        if (stalkPauseUntil > 0f)
        {
            // pause just ended
            stalkPauseUntil = 0f;
            agent.isStopped = false;
        }

        if (nextStalkPauseTime <= 0f) nextStalkPauseTime = Time.time + Random.Range(15f, 30f);

        // Only when the player is somewhere nearby (he "senses" them) - never across the whole map
        if (Time.time >= nextStalkPauseTime && Vector3.Distance(transform.position, playerTransform.position) < 22f)
        {
            nextStalkPauseTime = Time.time + Random.Range(20f, 40f);
            stalkPauseUntil = Time.time + Random.Range(2.5f, 4f);
            if (patrolVoiceLines != null && patrolVoiceLines.Length > 0 && enemyAudioSource != null)
            {
                enemyAudioSource.PlayOneShot(patrolVoiceLines[Random.Range(0, patrolVoiceLines.Length)]);
                nextVoiceTime = Time.time + Random.Range(minVoiceDelay, maxVoiceDelay);
            }
            return true;
        }
        return false;
    }

    private void ChaseBehavior()
    {
        agent.speed = chaseSpeed;
        // Stop a little in front of the player instead of walking INTO them (the model used to fill the camera)
        agent.stoppingDistance = chaseStandOffDistance;

        // Just spotted the player: stands still and screams first (gives a split second to run)
        if (Time.time < chaseYellUntil && playerTransform != null)
        {
            agent.isStopped = true;
            Vector3 toPlayer = playerTransform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer), Time.deltaTime * 8f);
            lastKnownPosition = playerTransform.position;
            return;
        }
        HandleFootsteps(0.3f); // Faster footsteps during chase

        if (enemyAudioSource != null && chaseMusic != null && enemyAudioSource.clip != chaseMusic)
        {
            enemyAudioSource.clip = chaseMusic;
            enemyAudioSource.Play();
        }

        // BUG FIX: pehle enemy chhupe hue player ko bhi seedha dhoond kar maar deta tha, aur deewar ke peeche bhi
        // hamesha player ki asal position jaanta tha. Ab sirf tab follow karta hai jab player nazar aa raha ho.
        bool playerHidden = playerController != null && playerController.isHidden;
        bool playerVisible = !playerHidden && HasLineOfSightToPlayer();

        if (playerVisible)
        {
            lostSightTimer = 0f;
            lastKnownPosition = playerTransform.position;
        }
        else
        {
            lostSightTimer += Time.deltaTime;
        }

        agent.SetDestination(playerVisible ? playerTransform.position : lastKnownPosition);

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (playerHidden || distanceToPlayer > detectionRange || lostSightTimer > loseSightTime)
        {
            currentState = AIState.Search;
            searchTimer = searchDuration;
            ResetSearchPlan();
            lostSightTimer = 0f;
            agent.updateRotation = true;

            // NEW: Player bach nikla! Darr ki wajah se saans phoolay gi
            if (playerStats != null)
            {
                playerStats.TriggerHeavyBreathing(6f);
            }

            return;
        }

        float heightDifference = Mathf.Abs(playerTransform.position.y - transform.position.y);
        if (playerVisible && distanceToPlayer <= attackRange && heightDifference <= maxAttackHeightDifference)
        {
            agent.updateRotation = false; // NavMesh ko ghumnay se roko!
            
            // Faza mein/Attack ke doran bilkul player ki taraf rukh kare (Face the player)
            Vector3 direction = (playerTransform.position - transform.position).normalized;
            direction.y = 0; // Sirf Y-axis par ghoomna chahiye (left/right)
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion lookRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 15f); // Faster rotation
            }

            AttackPlayer();
        }
        else
        {
            agent.updateRotation = true; // Wapis NavMesh ko control de do
        }

        // Failsafe: Agar rasta toota hua hai (NavMesh partial) aur player tak nahi pahunch pa raha
        if (agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathPartial && Vector3.Distance(transform.position, playerTransform.position) > 3f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 5f) // 5 second tak koshish kare, phir give up!
            {
                Debug.LogWarning("Player is unreachable (PathPartial). Enemy is giving up chase and returning to Patrol.");
                currentState = AIState.Patrol;
                GetNewPatrolTarget();
                stuckTimer = 0f;
                ignorePlayerTimer = 10f; // Ignore player so he actually walks away
                return;
            }
        }
        else if (agent.velocity.sqrMagnitude > 0.1f)
        {
            stuckTimer = 0f; // Agar sahi se chal raha hai to timer reset
        }
    }

    private void ResetSearchPlan()
    {
        searchPhase = 0;
        favouriteChecked = false;
        spotLingerTimer = 0f;
    }

    /// <summary>
    /// SEARCH (adaptive): 1) walk to where the player was last seen, 2) at aggression 4+ also check the player's
    /// favourite hiding spot (the one they used most), 3) sweep random points around the area until the timer runs out.
    /// At aggression 5 a player hiding in that favourite spot is found and pulled out.
    /// (Old version set the destination to the last known position every frame, so the random sweep never happened.)
    /// </summary>
    private void SearchBehavior()
    {
        agent.speed = patrolSpeed;
        HandleFootsteps();
        ResetAudioToAmbient();

        if (CanSeePlayer())
        {
            BeginChase();
            return;
        }

        bool arrived = !agent.pathPending && (agent.remainingDistance < 1f || agent.pathStatus == NavMeshPathStatus.PathInvalid || !agent.hasPath);

        if (searchPhase == 0)
        {
            if (!agent.hasPath || Vector3.Distance(agent.destination, lastKnownPosition) > 0.5f) agent.SetDestination(lastKnownPosition);
            if (arrived)
            {
                bool checkSpot = hasFavouriteSpot && !favouriteChecked && AggressionLevel >= 4 &&
                                 Vector3.Distance(transform.position, favouriteSpot) < 25f;
                searchPhase = checkSpot ? 1 : 2;
                if (checkSpot)
                {
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(favouriteSpot, out hit, 2.5f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
                    else searchPhase = 2;
                    spotLingerTimer = 2.5f;
                }
            }
            return;
        }

        if (searchPhase == 1)
        {
            float d = Vector3.Distance(transform.position, favouriteSpot);
            if (arrived || d < 1.8f)
            {
                agent.isStopped = true;
                spotLingerTimer -= Time.deltaTime;
                Vector3 look = favouriteSpot - transform.position; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 4f);

                bool playerInSpot = playerController != null && playerController.isHidden && playerTransform != null &&
                                    Vector3.Distance(playerTransform.position, favouriteSpot) < 2.0f;
                if (playerInSpot && AggressionLevel >= 5 && spotLingerTimer < 1.2f)
                {
                    // He knows this is where the player always hides
                    agent.isStopped = false;
                    favouriteChecked = true;
                    playerController.ExitHiding();
                    lastKnownPosition = playerTransform.position;
                    BeginChase();
                    return;
                }
                if (spotLingerTimer <= 0f)
                {
                    agent.isStopped = false;
                    favouriteChecked = true;
                    searchPhase = 2;
                }
            }
            return;
        }

        // phase 2: sweep around the last known position
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0)
        {
            currentState = AIState.Patrol;
            GetNewPatrolTarget();
            return;
        }
        if (arrived)
        {
            Vector3 randomSearchPoint = lastKnownPosition + Random.insideUnitSphere * searchRadius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomSearchPoint, out hit, searchRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    private bool CanSeePlayer()
    {
        if (playerTransform == null) return false;
        if (ignorePlayerTimer > 0) return false; // Ignore player temporally!
        
        if (playerController != null && playerController.isHidden) return false; // Enemy cannot see hidden player

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer > detectionRange) return false;

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

        return angleToPlayer < fovAngle / 2f && HasLineOfSightToPlayer();
    }

    /// <summary>True when no wall (obstacleMask) is between the enemy's eyes and the player.</summary>
    private bool HasLineOfSightToPlayer()
    {
        if (playerTransform == null) return false;

        Vector3 eye = transform.position + Vector3.up;
        Vector3 toPlayer = playerTransform.position - eye;
        float distance = toPlayer.magnitude;
        if (distance < 0.01f) return true;

        RaycastHit hit;
        if (!Physics.Raycast(eye, toPlayer / distance, out hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return true; // Raste mein koi deewar nahi
        }
        return hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform); // Raycast player se takrai hai
    }

    private void AttackPlayer()
    {
        if (Time.time < nextAttackTime) return;

        // ANIMATION FIX: the attack clip is ~7.5 s long. Re-triggering it every 'attackRate' seconds restarted it over and over
        // (he never finished a swing), and the damage landed before the swing even started. Now: one full swing per attack,
        // damage at the moment of the slam, and the next attack only after the swing has ended.
        if (enemyAnimator != null)
        {
            enemyAnimator.ResetTrigger("Tease");
            enemyAnimator.SetTrigger("Attack");
        }

        nextAttackTime = Time.time + Mathf.Max(attackRate, attackAnimLength);
        StartCoroutine(AttackHitRoutine());
    }

    private System.Collections.IEnumerator AttackHitRoutine()
    {
        yield return new WaitForSeconds(attackHitDelay);

        // Something else took over (stun, cinematic, he lost the player...) - the swing does nothing
        if (currentState != AIState.Chase || playerTransform == null || playerStats == null) yield break;
        if (playerStats.IsDead()) yield break;

        bool playerHidden = playerController != null && playerController.isHidden;
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        float height = Mathf.Abs(playerTransform.position.y - transform.position.y);
        if (playerHidden || dist > attackRange * 1.4f || height > maxAttackHeightDifference) yield break; // player stepped away in time

        playerStats.TakeDamage(damageAmount);
        if (playerStats.IsDead() && currentState != AIState.VanishAfterKill)
        {
            StartCoroutine(VanishRoutine());
        }
    }

    /// <summary>Clears queued Attack/Tease triggers so they do not fire later (after a stun or a cinematic).</summary>
    private void ResetAnimTriggers()
    {
        if (enemyAnimator == null) return;
        enemyAnimator.ResetTrigger("Attack");
        enemyAnimator.ResetTrigger("Tease");
    }

    private System.Collections.IEnumerator VanishRoutine()
    {
        currentState = AIState.VanishAfterKill;
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        
        // Stare down at player for 1.5 seconds
        yield return new WaitForSeconds(1.5f);
        
        // Vanish!
        gameObject.SetActive(false);
    }

    private void GetNewPatrolTarget()
    {
        if (!agent.isOnNavMesh) return;

        Vector3 searchCenter = transform.position;

        // Granny Sense: 40% chance hai ke enemy randomly wander karte hue player ki aam disha (general area) ki taraf aaye
        if (playerTransform != null && Random.value < 0.4f)
        {
            searchCenter = Vector3.Lerp(transform.position, playerTransform.position, Random.Range(0.2f, 0.7f));
        }

        // Use insideUnitCircle for X and Z to avoid picking a point high up in the air
        Vector2 randomCircle = Random.insideUnitCircle * 15f;
        Vector3 randomDirection = new Vector3(randomCircle.x, 0, randomCircle.y);
        randomDirection += searchCenter;

        
        NavMeshHit hit;
        // Search distance barha diya (30f) taake fail na ho
        if (NavMesh.SamplePosition(randomDirection, out hit, 30f, NavMesh.AllAreas))
        {
            patrolTarget = hit.position;
            agent.SetDestination(patrolTarget);
        }
        else
        {
            Debug.LogWarning("EnemyAI: NavMesh.SamplePosition fail ho gaya! Koi rasta nahi mila.");
        }
    }

    private void HandleFootsteps(float customInterval = -1f)
    {
        if (agent.velocity.magnitude < 0.1f) return;

        stepTimer -= Time.deltaTime;
        float currentInterval = customInterval > 0 ? customInterval : stepInterval;

        if (stepTimer <= 0)
        {
            if (enemyAudioSource != null && footstepSounds != null && footstepSounds.Length > 0)
            {
                enemyAudioSource.PlayOneShot(footstepSounds[Random.Range(0, footstepSounds.Length)]);
            }
            stepTimer = currentInterval;
        }
    }

    public void SetChaseSpeed(float speed)
    {
        chaseSpeed = speed;
    }

    public void SetDetectionRange(float range)
    {
        detectionRange = range;
    }

    /// <summary>
    /// ADAPTIVE AI: applied by PlayerBehaviorTracker after every confirmed evaluation.
    /// Level 3 = Inspector values. Each level up/down changes chase speed, detection range, search time and search area.
    /// </summary>
    public void ApplyAggression(int level, float chaseSpeedBonus, float detectionBonus)
    {
        if (BaseChaseSpeed <= 0f) // called before Awake (enemy object still inactive in the scene)
        {
            BaseChaseSpeed = chaseSpeed; BaseDetectionRange = detectionRange;
            BaseSearchDuration = searchDuration; BaseSearchRadius = searchRadius;
        }
        AggressionLevel = Mathf.Clamp(level, 1, 5);
        int step = AggressionLevel - 3;
        chaseSpeed = Mathf.Max(1.5f, BaseChaseSpeed + step * 0.35f + chaseSpeedBonus);
        detectionRange = Mathf.Max(5f, BaseDetectionRange + step * 2.5f + detectionBonus);
        searchDuration = Mathf.Max(2f, BaseSearchDuration * (1f + step * 0.25f));
        searchRadius = Mathf.Max(2f, BaseSearchRadius * (1f + step * 0.2f));
    }

    /// <summary>ADAPTIVE AI: the hiding spot the player uses most (the enemy checks it first when aggression is 4 or more).</summary>
    public void SetFavouriteHidingSpot(bool has, Vector3 spot)
    {
        hasFavouriteSpot = has;
        favouriteSpot = spot;
    }

    public float CurrentChaseSpeed => chaseSpeed;
    public float CurrentDetectionRange => detectionRange;

    public void ForceAwaken(Vector3 targetLocation)
    {
        if (currentState == AIState.Tease || currentState == AIState.LeaveTease) 
        {
            Debug.Log("Enemy is teasing, ignoring ForceAwaken!");
            return; // Don't interrupt the cinematic tease!
        }

        // Don't break a running stun or the ending cinematic either
        if (currentState == AIState.Stunned || currentState == AIState.CinematicEscape) return;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent == null) 
        {
            Debug.LogError("EnemyAI: NavMeshAgent is missing! Cannot awaken.");
            return;
        }

        Debug.Log("EnemyAI: FORCED AWAKEN! Hunting player directly!");
        currentState = AIState.Chase;
        
        if (agent.isActiveAndEnabled && agent.isOnNavMesh) 
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed * 1.2f; // Sprints even faster when awakened!
            
            // Agar dushman bohat door hai, to usay thora qareeb teleport kar do taake chase mazedar ho
            if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) > 25f)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(playerTransform.position - (playerTransform.forward * 15f), out hit, 10f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    Debug.Log("Enemy teleported closer for the awakening chase!");
                }
            }
        }
        
        // Ensure scary music plays
        if (enemyAudioSource != null && chaseMusic != null)
        {
            enemyAudioSource.clip = chaseMusic;
            enemyAudioSource.Play();
        }
    }

    public void ForcePatrol()
    {
        Debug.Log("EnemyAI: Forced to Patrol! Ending any teases.");
        currentState = AIState.Patrol;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
        GetNewPatrolTarget();
    }

    // --- NEW: STAIRCASE TEASING (CINEMATIC) ---
    public void StartTeasing(Transform targetToLookAt, float teaseDuration, Transform leaveTarget = null)
    {
        if (agent == null) return;
        
        currentState = AIState.Tease;
        teaseTarget = targetToLookAt;
        teaseTimer = teaseDuration;
        teaseLeaveTarget = leaveTarget;
        
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true; // Stop moving!
        }

        // Tease animation chalayen (Jumpscare/Roar)
        if (enemyAnimator != null)
        {
            enemyAnimator.SetTrigger("Tease");
        }
        
        // Play a random creepy voice line
        if (enemyAudioSource != null && teaseVoiceLines != null && teaseVoiceLines.Length > 0)
        {
            AudioClip taunt = teaseVoiceLines[Random.Range(0, teaseVoiceLines.Length)];
            enemyAudioSource.PlayOneShot(taunt);
            Debug.Log("Enemy is teasing the player!");
        }
    }

    private void TeaseBehavior()
    {
        // Keep looking at the player while standing still
        if (teaseTarget != null)
        {
            Vector3 direction = (teaseTarget.position - transform.position).normalized;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion lookRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
            }
        }

        teaseTimer -= Time.deltaTime;
        if (teaseTimer <= 0)
        {
            // Done teasing, go back to normal patrol and ignore player for 1 second so he doesn't instantly insta-kill you, but chases sooner.
            ignorePlayerTimer = 1f; // Pehle 10 seconds tha, ab 1 second kar diya!
            
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                if (teaseLeaveTarget != null)
                {
                    NavMeshPath path = new NavMeshPath();
                    agent.CalculatePath(teaseLeaveTarget.position, path);
                    
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        agent.SetDestination(teaseLeaveTarget.position);
                        hasSetLeaveDestination = true;
                        leaveStuckTimer = 0f;
                        currentState = AIState.LeaveTease; // Walk away normally
                    }
                    else
                    {
                        // NavMesh is broken at stairs! Warp him immediately so he doesn't freeze.
                        Debug.LogWarning("NavMesh path is broken to LeaveTarget! Warping immediately.");
                        agent.Warp(teaseLeaveTarget.position);
                        EndTeaseState();
                    }
                }
                else
                {
                    EndTeaseState();
                }
            }
        }
    }

    private void LeaveTeaseBehavior()
    {
        if (teaseLeaveTarget == null)
        {
            EndTeaseState();
            return;
        }

        agent.speed = patrolSpeed;
        if (!hasSetLeaveDestination)
        {
            agent.SetDestination(teaseLeaveTarget.position);
            hasSetLeaveDestination = true;
        }
        
        HandleFootsteps();
        ResetAudioToAmbient();

        // Safely ignore player during this state
        ignorePlayerTimer = 1f;

        // Agar pahunch gaya
        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            Debug.Log("Enemy reached LeaveTarget. Ending tease state.");
            EndTeaseState();
            return;
        }

        // Failsafe: Agar phir bhi phans gaya to 4 seconds baad teleport kar do
        leaveStuckTimer += Time.deltaTime;
        if (leaveStuckTimer > 4f)
        {
            Debug.LogWarning("Enemy stuck while trying to leave Tease. Warping to LeaveTarget!");
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.Warp(teaseLeaveTarget.position);
            }
            EndTeaseState();
            leaveStuckTimer = 0f;
        }
    }

    private void EndTeaseState()
    {
        ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
        if (pm != null && pm.currentPhase != ProgressionManager.GamePhase.Nightmare)
        {
            Debug.Log("Nightmare phase not active yet. Enemy is going back to sleep (Dormant) after tease.");
            currentState = AIState.Dormant;
            gameObject.SetActive(false); // Disappears!
        }
        else
        {
            Debug.Log("Nightmare phase is active. Enemy is returning to Patrol after tease.");
            currentState = AIState.Patrol;
            GetNewPatrolTarget();
        }
    }

    void OnGUI()
    {
        if (showDebugOverlay && agent != null && gameObject.activeInHierarchy)
        {
            GUI.color = Color.red;
            GUILayout.Label("--- ENEMY DEBUG ---");
            GUILayout.Label("State: " + currentState);
            GUILayout.Label("On NavMesh: " + agent.isOnNavMesh);
            GUILayout.Label("Has Path: " + agent.hasPath);
            GUILayout.Label("Path Status: " + agent.pathStatus);
            GUILayout.Label("Is Stopped: " + agent.isStopped);
            GUILayout.Label("Velocity: " + agent.velocity.magnitude);
            GUILayout.Label("Agent Speed: " + agent.speed);
            GUILayout.Label("Remaining Dist: " + agent.remainingDistance);
            
            if (playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, playerTransform.position);
                Vector3 dir = (playerTransform.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, dir);
                GUILayout.Label($"Dist to Player: {dist:F1} (Range: {detectionRange})");
                GUILayout.Label($"Angle to Player: {angle:F1} (FOV: {fovAngle})");
                
                RaycastHit hit;
                bool hitWall = Physics.Raycast(transform.position + Vector3.up, dir, out hit, dist, obstacleMask);
                string hitName = hitWall ? hit.transform.name : "None";
                GUILayout.Label($"Raycast Hit: {hitName}");
            }
            else
            {
                GUILayout.Label("Player Transform: NULL!");
            }
            
            GUILayout.Label("Ignore Timer: " + ignorePlayerTimer);

            if (enemyAnimator != null)
            {
                GUILayout.Label("Animator Speed: " + enemyAnimator.GetFloat("Speed"));
            }
            GUI.color = Color.white;
        }
    }

    private void ResetAudioToAmbient()
    {
        if (enemyAudioSource != null)
        {
            // Agar pehle se chase music baj raha tha to use band karo
            if (ambientSound != null)
            {
                if (enemyAudioSource.clip != ambientSound)
                {
                    enemyAudioSource.clip = ambientSound;
                    enemyAudioSource.Play();
                }
            }
            else if (enemyAudioSource.clip == chaseMusic)
            {
                enemyAudioSource.Stop();
                enemyAudioSource.clip = null;
            }
        }
    }
}
