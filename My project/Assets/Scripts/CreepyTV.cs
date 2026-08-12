using UnityEngine;

public class CreepyTV : MonoBehaviour, IInteractable
{
    [Header("TV Settings")]
    [SerializeField] private string interactionPrompt = "Turn On TV";
    [SerializeField] private GameObject spookyTextObject; // The text that will appear on the TV screen
    [SerializeField] private AudioSource staticSound; // Optional: TV static or scary sound

    private bool isTVOn = false;

    public string GetInteractPrompt()
    {
        if (!isTVOn)
        {
            return interactionPrompt;
        }
        return string.Empty; // No prompt once it's already on
    }

    public void Interact(PlayerStats player)
    {
        if (isTVOn) return; // Do nothing if already on

        isTVOn = true;

        // Start the flickering animation coroutine
        if (spookyTextObject != null)
        {
            StartCoroutine(FlickerTextRoutine());
        }

        // Play a scary sound if assigned
        if (staticSound != null)
        {
            staticSound.Play();
        }

        Debug.Log("Creepy TV Turned On!");
    }

    private System.Collections.IEnumerator FlickerTextRoutine()
    {
        spookyTextObject.SetActive(true);
        
        // Infinite loop for continuous horror flickering
        while (true)
        {
            // Stay ON for a random duration
            yield return new WaitForSeconds(Random.Range(0.1f, 1.5f));
            
            // Glitch OFF
            spookyTextObject.SetActive(false);
            
            // Stay OFF for a tiny fraction of a second
            yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
            
            // Turn back ON
            spookyTextObject.SetActive(true);
        }
    }
}
