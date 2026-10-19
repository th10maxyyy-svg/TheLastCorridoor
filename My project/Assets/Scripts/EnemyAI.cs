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

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        BaseChaseSpeed = chaseSpeed;
        BaseDetectionRange = detectionRange;

        // Failsafe: Agar user enemy ko disable karna bhool jaye, to game shuru hote hi ye khud chupp jayega!
        // (Lekin agar kisi script ne isay jagane ke liye EnsureActive() call kiya hai to wapas band na ho)
        if (currentState == AIState.Dormant && !externallyActivated)
        {
            gameObject.SetActive(false);
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
            if (enemyAnimator != null) enemyAnimator.speed = 1f;
            transform.localEulerAngles = new Vector3(stunOriginalXRotation, transform.localEulerAngles.y, transform.localEulerAngles.z);
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

        // Animator ki speed update karein (taake walk/run automatically ho)
        if (enemyAnimator != null)
        {
            enemyAnimator.SetFloat("Speed", agent.velocity.magnitude);
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
        currentState = AIState.CinematicEscape;
        cinematicRunTarget = runTarget;
        cinematicRetreatTarget = retreatTarget;
        cinematicAnimationName = animationName;
        hasFlinched = false;
        
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed * cinematicRunSpeedMultiplier; // Fast aggressive run towards the door!
            agent.SetDestination(cinematicRunTarget.position);
        }
    }

    private void CinematicEscapeBehavior()
    {
        // 1. Running towards the light
        if (!hasFlinched)
        {
            HandleFootsteps(0.2f); // fast footsteps
            if (Vector3.Distance(transform.position, cinematicRunTarget.position) < cinematicStopDistance || 
               (!agent.pathPending && agent.remainingDistance < cinematicStopDistance))
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
                
                if (enemyAudioSource != null && teaseVoiceLines != null && teaseVoiceLines.Length > 0)
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
            HandleFootsteps(cinematicRetreatSpeedMultiplier > 0f ? 0.25f : 0.6f); // Slow footsteps (or panicked sprint)
        }
    }

    void LateUpdate()
    {
        if (enemyAnimator != null)
        {
            // Check if playing the attack animation
            AnimatorStateInfo stateInfo = enemyAnimator.GetCurrentAnimatorStateInfo(0);
            
            if (stateInfo.IsName("Hell Slammer A"))
            {
                // SIRF attack ke doran clown ko 90 degree ghuma do taake terha pan khtam ho jaye!
                enemyAnimator.transform.localRotation = Quaternion.Euler(0, attackRotationOffset, 0);
            }
            else
            {
                // Normal walk/run ke doran bilkul seedha rakho
                enemyAnimator.transform.localRotation = Quaternion.identity;
            }
            
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
            currentState = AIState.Chase;
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
            currentState = AIState.Chase;
            return;
        }

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

    private void ChaseBehavior()
    {
        agent.speed = chaseSpeed;
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

    private void SearchBehavior()
    {
        agent.speed = patrolSpeed;
        HandleFootsteps();
        ResetAudioToAmbient();

        if (CanSeePlayer())
        {
            currentState = AIState.Chase;
            return;
        }

        agent.SetDestination(lastKnownPosition);

        // Agar last location par pahunch gaya ya rasta nahi raha
        if (!agent.pathPending && (agent.remainingDistance < 1f || agent.pathStatus == NavMeshPathStatus.PathInvalid || !agent.hasPath))
        {
            searchTimer -= Time.deltaTime;

            if (searchTimer <= 0)
            {
                currentState = AIState.Patrol;
                GetNewPatrolTarget();
            }
            else
            {
                if (!agent.hasPath)
                {
                    Vector3 randomSearchPoint = lastKnownPosition + Random.insideUnitSphere * searchRadius;
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(randomSearchPoint, out hit, searchRadius, NavMesh.AllAreas))
                    {
                        agent.SetDestination(hit.position);
                    }
                }
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
        if (Time.time >= nextAttackTime)
        {
            // Animation chalayen
            if (enemyAnimator != null)
            {
                enemyAnimator.SetTrigger("Attack");
            }

            if (playerStats != null)
            {
                playerStats.TakeDamage(damageAmount);
                if (playerStats.IsDead() && currentState != AIState.VanishAfterKill)
                {
                    StartCoroutine(VanishRoutine());
                }
            }
            nextAttackTime = Time.time + attackRate;
        }
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
