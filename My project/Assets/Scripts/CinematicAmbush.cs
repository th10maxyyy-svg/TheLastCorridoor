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
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            
            // Fix: NavMeshAgent ko pehle band kar ke position set karni parti hai, warna Unity usay disable kar deta hai aur wo putla ban jata hai!
            if (agent != null) agent.enabled = false;
            
            enemy.transform.position = ambushSpawnPoint.position;
            enemy.gameObject.SetActive(true);
            
            if (agent != null) 
            {
                agent.enabled = true;
                agent.Warp(ambushSpawnPoint.position);
            }

            // Tell the enemy to Jumpscare or Chase!
            Transform playerTransform = null;

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
                if (useTeaseInsteadOfChase)
                {
                    enemy.StartTeasing(playerTransform, teaseDuration, teaseLeaveTarget);
                }
                else
                {
                    enemy.ForceAwaken(playerTransform.position);
                }
            }
            else
            {
                Debug.LogWarning("Ambush script couldn't find the Player by Script, Name, OR Tag!");
            }
            
            Debug.Log("Cinematic Ambush Started!");
        }
        else
        {
            Debug.LogWarning("Ambush script is missing the Enemy or Spawn Point reference!");
        }
    }
}
