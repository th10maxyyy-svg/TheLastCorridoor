using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState { Patrol, Chase, Search }

    [Header("State")]
    [SerializeField] private AIState currentState = AIState.Patrol;

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

    // Breadcrumb system for safe teleports
    private System.Collections.Generic.List<Vector3> playerBreadcrumbs = new System.Collections.Generic.List<Vector3>();
    private float breadcrumbTimer = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
            playerStats = player.GetComponent<PlayerStats>();
        }

        agent.speed = patrolSpeed;
    }

    void Update()
    {
        if (agent == null) return;

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
                return; // Jab tak NavMesh na mile, aagay ka code na chalaye
            }
        }

        // Ensure agent is not stopped
        if (agent.isStopped && (playerStats == null || !playerStats.IsDead()))
        {
            agent.isStopped = false;
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
            case AIState.Patrol:
                PatrolBehavior();
                break;
            case AIState.Chase:
                ChaseBehavior();
                break;
            case AIState.Search:
                SearchBehavior();
                break;
        }
    }

    private void PatrolBehavior()
    {
        agent.speed = patrolSpeed;

        if (CanSeePlayer())
        {
            Debug.Log("EnemyAI: Player nazar aa gaya, ab Chase karega!");
            currentState = AIState.Chase;
            return;
        }

        // --- RELENTLESS STALKER FIX ---
        // Agar player door bhag gaya hai aur nazar nahi aa raha, toh enemy phir bhi player ki taraf dheere dheere chalega
        if (Vector3.Distance(transform.position, playerTransform.position) > detectionRange)
        {
            agent.SetDestination(playerTransform.position);
            return;
        }

        // Check if stuck against a wall
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

        lastKnownPosition = playerTransform.position;

        if (Vector3.Distance(transform.position, playerTransform.position) > detectionRange)
        {
            currentState = AIState.Search;
            searchTimer = searchDuration;
            return;
        }

        if (Vector3.Distance(transform.position, playerTransform.position) <= attackRange)
        {
            AttackPlayer();
        }

        // --- NAVMESH GAP FIX ---
        // Agar rooms ke beech me gap hai, to agent ka path "Partial" ho jata hai.
        // Lekin agar wo player ke bohat qareeb hai (attack range me), to gap jump nahi karni warna uske oopar chad jayega!
        if (agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathPartial && Vector3.Distance(transform.position, playerTransform.position) > 5f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 1.5f)
            {
                // Player ki taraf thoda aagay teleport kar dein taake gap cross kar le
                Vector3 jumpDir = (playerTransform.position - transform.position).normalized;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position + jumpDir * 3.5f, out hit, 5f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    Debug.Log("Enemy ne NavMesh ka gap jump kiya!");
                }
                stuckTimer = 0f;
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
                return true;
            }
            else if (hit.transform == playerTransform)
            {
                return true;
            }
        }

        return false;
    }

    private void AttackPlayer()
    {
        if (Time.time >= nextAttackTime)
        {
            if (playerStats != null)
            {
                playerStats.TakeDamage(damageAmount);
            }
            nextAttackTime = Time.time + attackRate;
        }
    }

    private void GetNewPatrolTarget()
    {
        if (!agent.isOnNavMesh) return;

        // Use insideUnitCircle for X and Z to avoid picking a point high up in the air
        Vector2 randomCircle = Random.insideUnitCircle * 15f;
        Vector3 randomDirection = new Vector3(randomCircle.x, 0, randomCircle.y);
        randomDirection += transform.position;
        
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

    public void SetChaseSpeed(float speed)
    {
        chaseSpeed = speed;
    }

    public void SetDetectionRange(float range)
    {
        detectionRange = range;
    }
}
