using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class GlassShardTrap : MonoBehaviour
{
    [Header("Trap Settings")]
    [SerializeField] private float damageAmount = 15f;
    [SerializeField] private float noiseRadius = 25f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip glassBreakSound;

    private float lastTriggerTime = 0f;
    private float cooldownTime = 1f; // 1 second cooldown

    void Start()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        col.isTrigger = true; // Ensure it acts as a trigger zone

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D Sound
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time < lastTriggerTime + cooldownTime) return; // Cooldown check

        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
        {
            TriggerTrap(other.gameObject);
        }
    }

    private void TriggerTrap(GameObject player)
    {
        lastTriggerTime = Time.time;
        Debug.Log("Player stepped on glass shards!");

        // 1. Play Sound
        if (glassBreakSound != null)
        {
            audioSource.PlayOneShot(glassBreakSound);
        }

        // 2. Deal Damage
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.TakeDamage(damageAmount);
        }

        // 3. Alert Enemy (Noise Event)
        EnemyAI enemyAI = FindAnyObjectByType<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.HearNoise(transform.position);
        }
        
        // Removed the line that disabled the collider, so it can be triggered again!
    }
}
