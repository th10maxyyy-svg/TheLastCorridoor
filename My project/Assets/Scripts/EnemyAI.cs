using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState { Dormant, Patrol, Chase, Search, InvestigateSound, Tease, LeaveTease, VanishAfterKill, Stunned }

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

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        
        // Failsafe: Agar user enemy ko disable karna bhool jaye, to game shuru hote hi ye khud chupp jayega!
        if (currentState == AIState.Dormant)
        {
            gameObject.SetActive(false);
        }
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
        if (agent.isStopped && currentState != AIState.Dormant && currentState != AIState.Tease && currentState != AIState.LeaveTease && currentState != AIState.Stunned && (playerStats == null || !playerStats.IsDead()))
        {
            agent.isStopped = false;
        }

        // Animator ki speed update karein (taake walk/run automatically ho)
        if (enemyAnimator != null)
        {
            enemyAnimator.SetFloat("Speed", agent.velocity.magnitude);
        }

        // Start patrolling once the agent is on the NavMesh
        if (!hasInitializedPatrol)
        {
            Debug.Log("EnemyAI: Pehli dafa Patrol target dhoond raha hai...");
            GetNewPatrolTarget();
            hasInitializedPatrol = true;
        }

        // Handle game over / player death safely
        if (playerTransform == null)
        {
            Debug.LogWarning("EnemyAI: Player nahi mila! Enemy isliye nahi chal raha.");
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

    public void HearNoise(Vector3 noiseLocation)
    {
        // Don't get distracted by a sound if already chasing the player
        if (currentState == AIState.Chase) return;
        
        // Don't hear it if it's too far away
        if (Vector3.Distance(transform.position, noiseLocation) > detectionRange * 2f) return;

        Debug.Log("EnemyAI: Heard a noise! Investigating...");
        soundInvestigationPoint = noiseLocation;
        currentState = AIState.InvestigateSound;
        currentInvestigateDuration = 3f; // Default search time
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(noiseLocation, out hit, 5f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            agent.speed = chaseSpeed * 0.8f; // Walk fast to the sound, but not full sprint
        }
    }

    public void ForceInvestigate(Vector3 noiseLocation, float searchTime = 15f)
    {
        Debug.Log("EnemyAI: FORCED to investigate loud noise!");
        soundInvestigationPoint = noiseLocation;
        currentState = AIState.InvestigateSound;
        currentInvestigateDuration = searchTime; // Custom search time
        
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
        if (currentState == AIState.Dormant || currentState == AIState.VanishAfterKill) return;
        StartCoroutine(StunRoutine(duration));
    }
    
    private System.Collections.IEnumerator StunRoutine(float duration)
    {
        AIState previousState = currentState;
        currentState = AIState.Stunned;
        
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        
        if (enemyAnimator != null)
        {
            enemyAnimator.speed = 0f; // Freeze animation
        }
        
        // Dushman ko zameen par lita dein (Rotate 90 degrees backward)
        float originalXRotation = transform.localEulerAngles.x;
        transform.localEulerAngles = new Vector3(90f, transform.localEulerAngles.y, transform.localEulerAngles.z);
        
        yield return new WaitForSeconds(duration);
        
        // Dushman ko wapas khara karein
        transform.localEulerAngles = new Vector3(originalXRotation, transform.localEulerAngles.y, transform.localEulerAngles.z);
        
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
        agent.SetDestination(playerTransform.position);
        HandleFootsteps(0.3f); // Faster footsteps during chase

        if (enemyAudioSource != null && chaseMusic != null && enemyAudioSource.clip != chaseMusic)
        {
            enemyAudioSource.clip = chaseMusic;
            enemyAudioSource.Play();
        }

        lastKnownPosition = playerTransform.position;

        if (Vector3.Distance(transform.position, playerTransform.position) > detectionRange)
        {
            currentState = AIState.Search;
            searchTimer = searchDuration;
            
            // NEW: Player bach nikla! Darr ki wajah se saans phoolay gi
            if (playerStats != null)
            {
                playerStats.TriggerHeavyBreathing(6f);
            }
            
            return;
        }

        if (Vector3.Distance(transform.position, playerTransform.position) <= attackRange)
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
        
        PlayerController pc = playerTransform.GetComponent<PlayerController>();
        if (pc != null && pc.isHidden) return false; // Enemy cannot see hidden player

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer > detectionRange) return false;

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

        if (angleToPlayer < fovAngle / 2f)
        {
            RaycastHit hit;
            if (!Physics.Raycast(transform.position + Vector3.up, directionToPlayer, out hit, distanceToPlayer, obstacleMask))
            {
                return true; // Raste mein koi deewar nahi
            }
            else if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform))
            {
                return true; // Raycast player se ya player ke kisi hisse se takrai hai
            }
        }

        return false;
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
                        currentState = AIState.Patrol;
                        GetNewPatrolTarget();
                    }
                }
                else
                {
                    currentState = AIState.Patrol;
                    GetNewPatrolTarget();
                }
            }
        }
    }

    private void LeaveTeaseBehavior()
    {
        if (teaseLeaveTarget == null)
        {
            currentState = AIState.Patrol;
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
            Debug.Log("Enemy reached LeaveTarget. Returning to Patrol.");
            currentState = AIState.Patrol;
            GetNewPatrolTarget();
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
            currentState = AIState.Patrol;
            GetNewPatrolTarget();
            leaveStuckTimer = 0f;
        }
    }

    void OnGUI()
    {
        if (agent != null && gameObject.activeInHierarchy)
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
