using UnityEngine;

public class PlayerBehaviorTracker : MonoBehaviour
{
    [Header("DDA Settings")]
    [SerializeField] private float updateInterval = 10f; 

    private PlayerController playerController;
    private PlayerStats playerStats;
    private EnemyAI enemyAI;

    private float totalPlayTime = 0f;
    private float sprintTime = 0f;
    private float crouchTime = 0f;

    private float lastCheckTime = 0f;

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        playerStats = GetComponent<PlayerStats>();
        enemyAI = FindAnyObjectByType<EnemyAI>();
    }

    void Update()
    {
        if (playerStats == null || playerStats.IsDead()) return;

        totalPlayTime += Time.deltaTime;

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
            enemyAI = FindAnyObjectByType<EnemyAI>();
            if (enemyAI == null) return;
        }

        float sprintRatio = sprintTime / updateInterval;
        float crouchRatio = crouchTime / updateInterval;

        sprintTime = 0f;
        crouchTime = 0f;

        // Inspector wali values ko base banao (pehle 3.5 / 15 hard-coded the aur Inspector ki setting overwrite ho jati thi)
        float targetChaseSpeed = enemyAI.BaseChaseSpeed;
        float targetDetectionRange = enemyAI.BaseDetectionRange;

        if (sprintRatio > 0.4f)
        {
            targetChaseSpeed += 1.0f; // Pehle 1.5 tha, ab thoda kam badhega
            targetDetectionRange += 5.0f;
        }
        
        if (crouchRatio > 0.5f)
        {
            targetChaseSpeed -= 0.5f;
            targetDetectionRange -= 3.0f;
        }

        if (playerStats != null && playerStats.GetHealth() < 30f)
        {
            targetChaseSpeed -= 1.0f;
            targetDetectionRange -= 5.0f;
        }

        targetChaseSpeed = Mathf.Clamp(targetChaseSpeed, enemyAI.BaseChaseSpeed - 1f, enemyAI.BaseChaseSpeed + 2f);
        targetDetectionRange = Mathf.Clamp(targetDetectionRange, enemyAI.BaseDetectionRange - 5f, enemyAI.BaseDetectionRange + 10f);

        enemyAI.SetChaseSpeed(targetChaseSpeed);
        enemyAI.SetDetectionRange(targetDetectionRange);

        Debug.Log("[DDA] Enemy Speed: " + targetChaseSpeed + " | Range: " + targetDetectionRange);
    }
}
