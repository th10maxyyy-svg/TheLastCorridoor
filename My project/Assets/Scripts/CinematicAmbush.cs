using UnityEngine;
using UnityEngine.AI;

public class CinematicAmbush : MonoBehaviour
{
    [Header("Enemy Setup")]
    [Tooltip("The Enemy that will ambush the player")]
    public EnemyAI enemy;
    [Tooltip("Where the enemy should spawn when the ambush starts")]
    public Transform ambushSpawnPoint;

    [Header("Tease / Jumpscare Settings")]
    [Tooltip("The scary sound to play right when he spawns!")]
    public AudioClip ambushScream;
    [Tooltip("If true, plays a Jumpscare animation (Tease) instead of chasing.")]
    public bool useTeaseInsteadOfChase = true;
    [Tooltip("How long the enemy yells/teases before walking away")]
    public float teaseDuration = 3f;
    [Tooltip("Optional: Where the enemy walks away to after the jumpscare")]
    public Transform teaseLeaveTarget;

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

            // Tell the enemy to Jumpscare or Chase!
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                if (useTeaseInsteadOfChase)
                {
                    enemy.StartTeasing(player.transform, teaseDuration, teaseLeaveTarget);
                }
                else
                {
                    enemy.ForceAwaken(player.transform.position);
                }
            }
            
            Debug.Log("Cinematic Ambush Started!");
        }
        else
        {
            Debug.LogWarning("Ambush script is missing the Enemy or Spawn Point reference!");
        }
    }
}
