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
    [SerializeField] private Light trapLight; // The flickering point light

    private string saveFlag;

    private void Start()
    {
        // WORLD STATE: keypad se band kiya gaya trap respawn ke baad dobara chalu na ho
        saveFlag = "TrapDisabled_" + SaveManager.GetSceneObjectId(transform);
        if (SaveManager.instance != null && SaveManager.instance.HasFlag(saveFlag))
        {
            DisableTrap();
            return;
        }

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
                if (cc != null && cc.enabled)
                {
                    // BUG FIX: push AGAINST the way the player was walking (pushing away from the centre threw a sprinting
                    // player straight through the barrier)
                    Vector3 v = cc.velocity; v.y = 0f;
                    Vector3 pushDirection = v.sqrMagnitude > 0.01f ? -v.normalized : (other.transform.position - transform.position);
                    pushDirection.y = 0; // Keep push horizontal
                    cc.Move(pushDirection.normalized * 2f);
                }
            }
        }
    }

    public void DisableTrap()
    {
        isActive = false;

        if (SaveManager.instance != null && !string.IsNullOrEmpty(saveFlag)) SaveManager.instance.SetFlag(saveFlag);
        
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

        if (trapLight != null)
        {
            trapLight.enabled = false;
        }

        ProceduralSparkSound procSound = GetComponent<ProceduralSparkSound>();
        if (procSound != null)
        {
            procSound.isEnabled = false;
        }

        Debug.Log("Electric Trap Disabled!");
    }
}
