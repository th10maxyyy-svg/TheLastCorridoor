using UnityEngine;

public class KeyItem : MonoBehaviour, IInteractable
{
    [Header("Key Settings")]
    [SerializeField] private string keyName = "RedKey";
    [SerializeField] private string promptMessage = "Press E to pick up Key";

    [Header("Horror Events")]
    [Tooltip("If true, picking up this key will wake up the enemy and start the Nightmare Phase!")]
    [SerializeField] private bool wakesUpEnemy = false;

    [Header("Custom Event")]
    [Tooltip("Add things here that should happen when this key is picked up (e.g. Turn off lights, Spawn enemies)")]
    public UnityEngine.Events.UnityEvent onPickUpEvent;

    void Start()
    {
        // WORLD STATE CHECK: Agar ye key pehle hi uthai ja chuki hai, to isay hamesha ke liye delete kar do!
        if (SaveManager.instance != null && SaveManager.instance.IsItemCollected(gameObject.name))
        {
            // Lekin agar ye wo key hai jo marte waqt player ne girai thi, to ise delete mat karo (kyun ke RespawnManager ise sahi jagah move karega)
            if (SaveManager.instance.currentSaveData.droppedItemName != gameObject.name)
            {
                Destroy(gameObject);
                Debug.Log("[WorldState] KeyItem deleted because it was already collected: " + keyName);
            }
        }
    }

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

            // Trigger any custom events
            if (onPickUpEvent != null)
            {
                onPickUpEvent.Invoke();
            }

            // Show notification on screen
            InteractionRaycast ir = Camera.main.GetComponent<InteractionRaycast>();
            if (ir != null)
            {
                ir.ShowNotification("Picked up: " + keyName);
            }

            // Wake up enemy if this key triggers the nightmare phase
            if (wakesUpEnemy || keyName == "Staff Key")
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
