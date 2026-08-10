using UnityEngine;

public class HeadlampPickup : MonoBehaviour, IInteractable
{
    [Header("Pickup Settings")]
    [SerializeField] private string promptMessage = "Press E to equip Headlamp";
    
    [Header("Audio")]
    [SerializeField] private AudioClip pickupSound;

    public string GetInteractPrompt()
    {
        return promptMessage;
    }

    public void Interact(PlayerStats playerStats)
    {
        // Find the HeadlampController on the Player
        HeadlampController headlampController = playerStats.GetComponentInChildren<HeadlampController>();
        
        if (headlampController != null)
        {
            headlampController.PickupHeadlamp();
            
            // Play sound at the location before destroying
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }

            // Destroy the pickup object from the floor
            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("HeadlampController script not found on Player!");
        }
    }
}
