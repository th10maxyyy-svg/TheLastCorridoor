using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState { Patrol, Chase, Search }

    [Header("State")]
    [SerializeField] private AIState currentState = AIState.Patrol;

    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private float fovAngle = 90f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Speed Settings")]
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float chaseSpeed = 5.0f;

    [Header("Search Settings")]
    [SerializeField] private float searchDuration = 5f;
    [SerializeField] private float searchRadius = 5f;

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float damageAmount = 25f;
    [SerializeField] private float attackRate = 1.5f;

    // References
    private NavMeshAgent agent;
    private Transform playerTransform;
    private PlayerStats playerStats;

    // Internal State vars
    private Vector3 lastKnownPosition;
    private float searchTimer;
    private float nextAttackTime;
    private Vector3 patrolTarget;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
            playerStats = player.GetComponent<PlayerStats>();
        }

        agent.speed = patrolSpeed;
        GetNewPatrolTarget();
    }

    void Update()
    {
        if (playerTransform == null || (playerStats != null && playerStats.IsDead()))
        {
            agent.isStopped = true;
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

        // Check if player is detected
        if (CanSeePlayer())
        {
            currentState = AIState.Chase;
            return;
        }

        // Move to patrol target
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            GetNewPatrolTarget();
        }
    }

    private void ChaseBehavior()
    {
        agent.speed = chaseSpeed;
        agent.SetDestination(playerTransform.position);

        // Keep updating last known position while chasing
        lastKnownPosition = playerTransform.position;

        // If player gets out of range
        if (Vector3.Distance(transform.position, playerTransform.position) > detectionRange)
        {
            currentState = AIState.Search;
            searchTimer = searchDuration;
            return;
        }

        // Attack if close enough
        if (Vector3.Distance(transform.position, playerTransform.position) <= attackRange)
        {
            AttackPlayer();
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

        // Head towards the last known position first
        agent.SetDestination(lastKnownPosition);

        // If reached last known position, search around randomly
        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            searchTimer -= Time.deltaTime;

            if (searchTimer <= 0)
            {
                currentState = AIState.Patrol;
                GetNewPatrolTarget();
            }
            else
            {
                // Find random points nearby to search
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
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer > detectionRange) return false;

        // Check angle
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

        if (angleToPlayer < fovAngle / 2f)
        {
            // Raycast check for line of sight (obstacles block vision)
            RaycastHit hit;
            if (!Physics.Raycast(transform.position + Vector3.up, directionToPlayer, out hit, distanceToPlayer, obstacleMask))
            {
                // Ray hit nothing, meaning clear line of sight to player
                return true;
            }
            else if (hit.transform == playerTransform)
            {
                // Ray hit player directly
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
        // Find a random point near the enemy to walk to
        Vector3 randomDirection = Random.insideUnitSphere * 15f;
        randomDirection += transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, 15f, NavMesh.AllAreas))
        {
            patrolTarget = hit.position;
            agent.SetDestination(patrolTarget);
        }
    }

    // Dynamic Difficulty Adjustment setters
    public void SetChaseSpeed(float speed)
    {
        chaseSpeed = speed;
    }

    public void SetDetectionRange(float range)
    {
        detectionRange = range;
    }
}
