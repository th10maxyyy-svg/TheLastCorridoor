using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ThrowableItem : MonoBehaviour
{
    [SerializeField] private AudioClip dropSound;
    [SerializeField] private float noiseRadius = 15f;
    private AudioSource audioSource;
    private bool hasDropped = false;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // 3D Sound
        audioSource.maxDistance = noiseRadius;
    }

    public void MarkAsDropped()
    {
        hasDropped = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasDropped && collision.relativeVelocity.magnitude > 1f)
        {
            hasDropped = false; // So it doesn't keep making noise when rolling
            
            if (dropSound != null)
            {
                audioSource.PlayOneShot(dropSound);
            }

            // Alert the enemy
            EnemyAI enemy = FindAnyObjectByType<EnemyAI>();
            if (enemy != null)
            {
                enemy.HearNoise(transform.position);
            }
        }
    }
}
