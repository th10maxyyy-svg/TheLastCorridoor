using UnityEngine;

public class KeyItem : MonoBehaviour, IInteractable
{
    [Header("Key Settings")]
    [SerializeField] private string keyName = "RedKey";
    [SerializeField] private string promptMessage = "Press E to pick up Key";

    public string GetKeyName()
    {
        return keyName;
    }

    public string GetInteractPrompt()
    {
        return promptMessage;
    }

    public void Interact(PlayerStats playerStats)
    {
        Inventory inventory = playerStats.GetComponent<Inventory>();
        if (inventory != null)
        {
            // Instead of destroying, pick up the physical object
            inventory.PickUpItem(this.gameObject);

            // Show notification on screen
            InteractionRaycast ir = Camera.main.GetComponent<InteractionRaycast>();
            if (ir != null)
            {
                ir.ShowNotification("Picked up: " + keyName);
            }

            // Special logic for progression
            if (keyName == "Staff Key")
            {
                ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
                if (pm != null)
                {
                    // Starts the Nightmare phase (wakens enemy)
                    pm.StartNightmarePhase();
                }
            }
        }
    }
}
