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
        Inventory inventory = playerStats.GetComponent<Inventory>();
        if (inventory != null)
        {
            inventory.AddItem(keyName);
            Destroy(gameObject); 
        }
    }
}
