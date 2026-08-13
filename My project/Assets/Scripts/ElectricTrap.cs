using UnityEngine;

public class ElectricTrap : MonoBehaviour
{
    [Header("Trap Settings")]
    [SerializeField] private float damageAmount = 25f;
    [SerializeField] private bool isActive = true;

    [Header("Effects")]
    [SerializeField] private AudioSource electricSound;
    [SerializeField] private ParticleSystem sparksParticle;
    [SerializeField] private GameObject electricVisuals; // The bars or lightning objects

    private void Start()
    {
        if (isActive && electricSound != null && !electricSound.isPlaying)
        {
            electricSound.Play();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;

        if (other.CompareTag("Player"))
        {
            PlayerStats stats = other.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.TakeDamage(damageAmount);
                Debug.Log("Player hit by Electric Trap!");
                
                // Add a small pushback effect so the player knows they hit a barrier
                CharacterController cc = other.GetComponent<CharacterController>();
                if (cc != null)
                {
                    Vector3 pushDirection = (other.transform.position - transform.position).normalized;
                    pushDirection.y = 0; // Keep push horizontal
                    cc.Move(pushDirection * 2f);
                }
            }
        }
    }

    public void DisableTrap()
    {
        isActive = false;
        
        if (electricSound != null)
        {
            electricSound.Stop();
        }

        if (sparksParticle != null)
        {
            sparksParticle.Stop();
        }

        if (electricVisuals != null)
        {
            electricVisuals.SetActive(false);
        }

        Debug.Log("Electric Trap Disabled!");
    }
}
