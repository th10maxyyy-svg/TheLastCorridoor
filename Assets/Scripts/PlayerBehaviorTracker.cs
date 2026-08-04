using UnityEngine;

public class PlayerBehaviorTracker : MonoBehaviour
{
    [Header("DDA Settings")]
    [SerializeField] private float updateInterval = 10f; // Tweak difficulty every 10 seconds

    // References
    private PlayerController playerController;
    private PlayerStats playerStats;
    private EnemyAI enemyAI;

    // Tracked metrics
    private float totalPlayTime = 0f;
    private float sprintTime = 0f;
    private float crouchTime = 0f;
    private int damageTakenCount = 0;

    private float lastCheckTime = 0f;

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        playerStats = GetComponent<PlayerStats>();
        
        // Find the enemy in the scene
        enemyAI = FindFirstObjectByType<EnemyAI>();
    }

    void Update()
    {
        if (playerStats == null || playerStats.IsDead()) return;

        totalPlayTime += Time.deltaTime;

        // Track behavior
        if (playerController != null)
        {
            if (playerController.IsSprinting)
            {
                sprintTime += Time.deltaTime;
            }
            if (playerController.IsCrouching)
            {
                crouchTime += Time.deltaTime;
            }
        }

        // Tweak difficulty periodically
        if (Time.time - lastCheckTime >= updateInterval)
        {
            AdjustDifficulty();
            lastCheckTime = Time.time;
        }
    }

    private void AdjustDifficulty()
    {
        if (enemyAI == null)
        {
            // Try to find enemy if not assigned
            enemyAI = FindFirstObjectByType<EnemyAI>();
            if (enemyAI == null) return;
        }

        // Calculate ratios
        float sprintRatio = sprintTime / updateInterval;
        float crouchRatio = crouchTime / updateInterval;

        // Reset trackers for the next interval
        sprintTime = 0f;
        crouchTime = 0f;

        // Default enemy AI base stats
        float targetChaseSpeed = 5.0f;
        float targetDetectionRange = 12f;

        // --- Difficulty Logic (Dynamic Difficulty Adjustment) ---
        // 1. If player is sprinting a lot (skillful/confident player) -> Make enemy faster
        if (sprintRatio > 0.4f)
        {
            targetChaseSpeed += 1.5f;
            targetDetectionRange += 3.0f;
        }
        
        // 2. If player is sneaking/crouching a lot (stealthy/cautious player) -> Make enemy slightly slower but look closer
        if (crouchRatio > 0.5f)
        {
            targetChaseSpeed -= 0.5f;
            targetDetectionRange -= 2.0f;
        }

        // 3. If player's health is low -> Give them a break (reduce speed)
        if (playerStats != null && playerStats.GetHealth() < 30f)
        {
            targetChaseSpeed -= 1.0f;
            targetDetectionRange -= 3.0f;
        }

        // Clamp values to reasonable limits
        targetChaseSpeed = Mathf.Clamp(targetChaseSpeed, 3.0f, 7.5f);
        targetDetectionRange = Mathf.Clamp(targetDetectionRange, 6f, 20f);

        // Apply to Enemy
        enemyAI.SetChaseSpeed(targetChaseSpeed);
        enemyAI.SetDetectionRange(targetDetectionRange);

        Debug.Log($"[DDA] Stats Adjusted. Sprint: {sprintRatio:F2}, Crouch: {crouchRatio:F2}. Enemy Speed: {targetChaseSpeed}, Enemy Range: {targetDetectionRange}");
    }
}
