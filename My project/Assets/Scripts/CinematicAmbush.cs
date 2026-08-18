using UnityEngine;
using UnityEngine.AI;

public class CinematicAmbush : MonoBehaviour
{
    [Header("Enemy Setup")]
    [Tooltip("The Enemy that will ambush the player")]
    public EnemyAI enemy;
    [Tooltip("Where the enemy should spawn when the ambush starts")]
    public Transform ambushSpawnPoint;

    [Header("Ambush Settings")]
    [Tooltip("The scary sound to play right when he spawns!")]
    public AudioClip ambushScream;
    private AudioSource audioSource;
    private bool hasAmbushed = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    // Call this from DoorController's "OnLockedInteract" Event!
    public void TriggerAmbush()
    {
        if (hasAmbushed) return; // Only ambush once!
        hasAmbushed = true;

        // Play the sudden scream!
        if (ambushScream != null)
        {
            audioSource.PlayOneShot(ambushScream);
        }

        if (enemy != null && ambushSpawnPoint != null)
        {
            // Turn on the enemy if it was off
            enemy.gameObject.SetActive(true);
            
            // Teleport the enemy right behind the player!
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.Warp(ambushSpawnPoint.position);
            }
            else
            {
                enemy.transform.position = ambushSpawnPoint.position;
            }

            // Tell the enemy to WAKE UP and CHASE immediately!
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                enemy.ForceAwaken(player.transform.position);
            }
            
            Debug.Log("Cinematic Ambush Started! RUN!!!");
        }
        else
        {
            Debug.LogWarning("Ambush script is missing the Enemy or Spawn Point reference!");
        }
    }
}
