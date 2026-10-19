using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource ambientSource;  
    [SerializeField] private AudioSource heartbeatSource;

    [Header("Tension Settings")]
    [SerializeField] private float maxTensionDistance = 20f; 
    [SerializeField] private float minTensionDistance = 3f;  

    private Transform playerTransform;
    private Transform enemyTransform;
    private float nextEnemySearchTime = 0f;

    void Start()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null) playerTransform = player.transform;

        if (ambientSource != null && !ambientSource.isPlaying)
        {
            ambientSource.loop = true;
            ambientSource.Play();
        }

        if (heartbeatSource != null)
        {
            heartbeatSource.loop = true;
            heartbeatSource.volume = 0f;
            heartbeatSource.Play();
        }
    }

    void Update()
    {
        // Enemy dhoondna mehnga hai - har frame nahi, second mein ek dafa
        if (enemyTransform == null && Time.time >= nextEnemySearchTime)
        {
            nextEnemySearchTime = Time.time + 1f;
            EnemyAI enemy = FindAnyObjectByType<EnemyAI>();
            if (enemy != null) enemyTransform = enemy.transform;
        }

        if (playerTransform == null || enemyTransform == null || heartbeatSource == null) return;

        float distance = Vector3.Distance(playerTransform.position, enemyTransform.position);

        if (distance < maxTensionDistance)
        {
            float tension = 1f - ((distance - minTensionDistance) / (maxTensionDistance - minTensionDistance));
            tension = Mathf.Clamp01(tension);

            heartbeatSource.volume = tension;
            heartbeatSource.pitch = Mathf.Lerp(1.0f, 1.8f, tension); 
        }
        else
        {
            heartbeatSource.volume = Mathf.MoveTowards(heartbeatSource.volume, 0f, Time.deltaTime);
        }
    }
}
