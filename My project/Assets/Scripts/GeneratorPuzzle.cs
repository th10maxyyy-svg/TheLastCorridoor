using UnityEngine;
using UnityEngine.Events;

public class GeneratorPuzzle : MonoBehaviour, IInteractable
{
    [Header("Generator Settings")]
    public string requiredItem = "Crank Handle";
    public bool isGeneratorOn = false;

    [Header("Audio & Visuals")]
    public AudioSource audioSource;
    public AudioClip startupSound;
    public AudioClip runningLoopSound;
    
    [Tooltip("Lights that should turn on when the generator starts")]
    public GameObject[] generatorLights;

    [Header("Events")]
    public UnityEvent onGeneratorStarted;

    public string GetInteractPrompt()
    {
        if (isGeneratorOn) return "Generator is running smoothly.";
        return "Press E to start Generator (Needs " + requiredItem + ")";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isGeneratorOn) return;

        Inventory inventory = playerStats.GetComponent<Inventory>();
        if (inventory != null && inventory.HasItem(requiredItem))
        {
            StartGenerator();
            
            // Show notification
            InteractionRaycast ir = Camera.main.GetComponent<InteractionRaycast>();
            if (ir != null)
            {
                ir.ShowNotification("Power Restored!");
            }
        }
        else
        {
            InteractionRaycast ir = Camera.main.GetComponent<InteractionRaycast>();
            if (ir != null)
            {
                ir.ShowNotification("You need a " + requiredItem + " to start this.");
            }
        }
    }

    private void StartGenerator()
    {
        isGeneratorOn = true;

        if (audioSource != null && startupSound != null)
        {
            audioSource.PlayOneShot(startupSound);
            // Wait for startup to finish then play loop
            if (runningLoopSound != null)
            {
                audioSource.clip = runningLoopSound;
                audioSource.loop = true;
                audioSource.PlayDelayed(startupSound.length);
            }
        }

        foreach (GameObject light in generatorLights)
        {
            if (light != null) light.SetActive(true);
        }

        onGeneratorStarted?.Invoke();
    }
}
