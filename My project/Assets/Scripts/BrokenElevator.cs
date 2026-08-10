using UnityEngine;

public class BrokenElevator : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    [SerializeField] private string displayMessage = "Elevator is Out of Order";
    
    [Header("Optional Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip errorSound;

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    public string GetInteractPrompt()
    {
        // Jab player isay dekhega to yeh text screen par aayega
        return displayMessage;
    }

    public void Interact(PlayerStats playerStats)
    {
        // Jab player 'E' dabayega to error sound aayega (agar set kiya ho)
        if (audioSource != null && errorSound != null)
        {
            audioSource.PlayOneShot(errorSound);
        }
        
        Debug.Log("Player tried to use broken elevator.");
    }
}
