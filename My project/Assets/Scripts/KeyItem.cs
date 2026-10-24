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

    private bool hasFiredPickupEvent = false;

    void Start()
    {
        // WORLD STATE CHECK: Agar ye key pehle hi uthai ja chuki hai (aur na zameen par giri hai na haath mein hai) to delete kar do
        if (SaveManager.instance != null && !SaveManager.instance.ShouldItemExistInWorld(SaveManager.instance.GetItemId(gameObject)))
        {
            Destroy(gameObject);
            Debug.Log("[WorldState] KeyItem deleted because it was already collected: " + keyName);
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

            // Trigger custom events only the FIRST time (not every time the key is dropped and picked up again)
            // BUG FIX: remembered in the save, so dropping it / dying and picking it up again never replays the event (ambush etc.)
            SaveManager sm = SaveManager.instance;
            string evtFlag = "KeyEvent_" + (sm != null ? sm.GetItemId(gameObject) : name);
            if (!hasFiredPickupEvent && (sm == null || !sm.HasFlag(evtFlag)))
            {
                hasFiredPickupEvent = true;
                if (sm != null) sm.SetFlag(evtFlag);
                onPickUpEvent?.Invoke();
            }

            // Show notification on screen
            InteractionRaycast.Notify(playerStats, "Picked up: " + keyName);

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
