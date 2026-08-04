using UnityEngine;

public class KeyItem : MonoBehaviour, IInteractable
{
    [Header("Key Settings")]
    [SerializeField] private string keyName = "RedKey";
    [SerializeField] private string promptMessage = "Press E to pick up Key";

    public string GetInteractPrompt()
    {
        return promptMessage;
    }

    public void Interact(PlayerStats playerStats)
    {
        // Try to find the Inventory component on the player
        Inventory inventory = playerStats.GetComponent<Inventory>();
        if (inventory != null)
        {
            inventory.AddItem(keyName);
            
            // Optional: Play pickup sound here
            
            // Destroy the key GameObject so it disappears from the ground
            Destroy(gameObject);
        }
        else
        {
            Debug.LogError("No Inventory script found on Player!");
        }
    }
}
