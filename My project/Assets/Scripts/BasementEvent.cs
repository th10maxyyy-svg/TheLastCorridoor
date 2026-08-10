using UnityEngine;

public class BasementEvent : MonoBehaviour, IInteractable
{
    [Header("Event Settings")]
    [SerializeField] private string requiredItem = "GeneratorFuse";
    [SerializeField] private string promptText = "Press E to Insert Fuse & Power Up";
    [SerializeField] private bool hasTriggered = false;

    [Header("Enemy Awakening")]
    [SerializeField] private EnemyAI enemyScript;
    
    [Header("Audio (BOOM / Alarm)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip loudBlastSound;

    [Header("Lighting (Optional)")]
    [SerializeField] private Light[] lightsToTurnOn;

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.maxDistance = 50f; // Make sure enemy hears it!
            }
        }
        
        // Find enemy automatically if not assigned
        if (enemyScript == null)
        {
            enemyScript = FindAnyObjectByType<EnemyAI>();
        }
    }

    public string GetInteractPrompt()
    {
        if (hasTriggered)
            return "Generator is already running!";
            
        return promptText;
    }

    public void Interact(PlayerStats playerStats)
    {
        if (hasTriggered) return;

        Inventory inventory = playerStats.GetComponent<Inventory>();
        
        // Agar required item zaroori nahi hai (empty string) ya player ke paas hai
        if (string.IsNullOrEmpty(requiredItem) || (inventory != null && inventory.HasItem(requiredItem)))
        {
            TriggerAwakeningEvent();
        }
        else
        {
            Debug.Log("Need " + requiredItem + " to do this!");
        }
    }

    public void TriggerAwakeningEvent()
    {
        hasTriggered = true;
        Debug.Log("Basement Event Triggered! Waking up the enemy...");

        // 1. Play Loud Sound
        if (audioSource != null && loudBlastSound != null)
        {
            audioSource.PlayOneShot(loudBlastSound);
        }

        // 2. Turn on lights (if any)
        if (lightsToTurnOn != null)
        {
            foreach (Light l in lightsToTurnOn)
            {
                if (l != null) l.enabled = true;
            }
        }

        // 3. AWAKEN THE ENEMY
        if (enemyScript != null)
        {
            // Enemy will run straight to this location!
            enemyScript.ForceAwaken(transform.position);
        }
        else
        {
            Debug.LogWarning("Enemy script not found for awakening!");
        }
    }
}
