using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class TeaseTrigger : MonoBehaviour
{
    [Header("Tease Settings")]
    [Tooltip("The Enemy that will stop and tease the player")]
    public EnemyAI enemy;
    [Tooltip("How many seconds should the enemy stand there and tease before patrolling?")]
    public float teaseDuration = 15f;

    [Tooltip("Where should the enemy walk away to after teasing? (e.g. deep inside 1st floor)")]
    public Transform leaveTarget;

    private bool hasTriggered = false;
    private PlayerController player;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        player = FindAnyObjectByType<PlayerController>();
    }

    void OnTriggerEnter(Collider other)
    {
        // Only trigger once, and only if the Player steps in it!
        if (!hasTriggered && (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null))
        {
            hasTriggered = true;

            if (enemy != null && player != null)
            {
                // Tell the enemy to stop chasing and start teasing!
                enemy.StartTeasing(player.transform, teaseDuration, leaveTarget);
                Debug.Log("Player entered TeaseTrigger. Enemy has stopped to tease!");
            }
        }
    }
}
