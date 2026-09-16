using UnityEngine;
using UnityEngine.Events;
public class DoorController : MonoBehaviour, IInteractable
{
    [Header("Lock Settings")]
    [SerializeField] private bool isLocked = false;
    [SerializeField] private string requiredKey = "RedKey";

    [Header("Movement Settings")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up; // (0,1,0) by default
    [SerializeField] private float openAngle = 90.0f;
    [SerializeField] private float smoothSpeed = 5.0f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    [Header("Events")]
    public UnityEvent onDoorOpen;
    [Tooltip("Fires when player tries to open but it's locked")]
    public UnityEvent onLockedInteract;
    
    [Header("Horror Events")]
    [Tooltip("If this door should trigger an ambush when unlocked, assign it here.")]
    public CinematicAmbush ambushToTrigger;

    private bool isOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    void Start()
    {
        closedRotation = transform.localRotation;
        
        // Calculate target rotation based on the chosen axis
        openRotation = closedRotation * Quaternion.AngleAxis(openAngle, rotationAxis);

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f; // Make it 3D sound
            }
        }

        // WORLD STATE CHECK: Agar ye darwaza pehle se unlock tha to isay automatically open kar do!
        if (SaveManager.instance != null && SaveManager.instance.IsDoorUnlocked(GetDoorID()))
        {
            isLocked = false;
            isOpen = true;
            transform.localRotation = openRotation; // Snap to open immediately
        }
    }

    void Update()
    {
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * smoothSpeed);
    }
    
    // Can be called by KeypadController's OnUnlock Event
    public void UnlockDoor()
    {
        isLocked = false;
        isOpen = true; // Automatically open when unlocked via keypad
        if (audioSource != null && openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }
        SaveDoorState();
    }

    public string GetInteractPrompt()
    {
        if (isLocked)
        {
            return "Door is Locked. Needs " + requiredKey + ".";
        }
        
        return isOpen ? "Press E to Close Door" : "Press E to Open Door";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isLocked)
        {
            // NAYA FIX: Agar Required Key empty hai, to matlab isay inventory se nahi, kisi Puzzle se khulna hai!
            if (string.IsNullOrEmpty(requiredKey))
            {
                Debug.Log("Door is locked via puzzle!");
                if (onLockedInteract != null) onLockedInteract.Invoke();
                return;
            }

            Inventory inventory = playerStats.GetComponent<Inventory>();
            if (inventory != null && inventory.HasItem(requiredKey))
            {
                isLocked = false;
                ToggleDoor();
                Debug.Log("Door unlocked and opened!");
                
                // NAYA FIX: Key use hone ke baad usay inventory se khatam (consume) kar do!
                inventory.RemoveItem(requiredKey);
                
                SaveDoorState();

                // Auto-Trigger Cinematic Ambush! 
                if (ambushToTrigger != null)
                {
                    ambushToTrigger.TriggerAmbush(true); // TRUE = Force Chase!
                }
                else if (!string.IsNullOrEmpty(requiredKey) && requiredKey.ToLower().Contains("guard"))
                {
                    // FALLBACK: Agar inspector mein assign karna bhool gaye to qareeb tareen ambush khud dhoondo!
                    CinematicAmbush[] ambushes = FindObjectsByType<CinematicAmbush>(FindObjectsSortMode.None);
                    CinematicAmbush closestAmbush = null;
                    float minDistance = Mathf.Infinity;
                    
                    foreach (var ambush in ambushes)
                    {
                        float dist = Vector3.Distance(transform.position, ambush.transform.position);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            closestAmbush = ambush;
                        }
                    }

                    if (closestAmbush != null && minDistance < 10f) // Door ke paas hona chahiye
                    {
                        closestAmbush.TriggerAmbush(true); // TRUE = Force Chase!
                        Debug.LogWarning("DoorController: ambushToTrigger inspector mein khali tha, lekin auto-fallback ne qareeb tareen ambush trigger kar diya!");
                    }
                    else
                    {
                        Debug.LogWarning("DoorController: Koi CinematicAmbush aas paas nahi mila!");
                    }
                }
            }
            else
            {
                Debug.Log("Door is locked!");
                if (onLockedInteract != null)
                {
                    onLockedInteract.Invoke();
                }
            }
        }
        else
        {
            ToggleDoor();
        }
    }

    private void ToggleDoor()
    {
        isOpen = !isOpen;

        // Play sound and trigger events
        if (isOpen)
        {
            if (audioSource != null && openSound != null)
                audioSource.PlayOneShot(openSound);
                
            // Naya code: Jab darwaza khulega to ye event trigger hoga
            if (onDoorOpen != null)
                onDoorOpen.Invoke();
        }
        else
        {
            if (audioSource != null && closeSound != null)
                audioSource.PlayOneShot(closeSound);
        }
    }
    
    // External method to unlock the door from puzzles (like a Generator)
    public void ForceUnlock()
    {
        isLocked = false;
        Debug.Log("Door " + gameObject.name + " unlocked externally!");
        SaveDoorState();
    }

    // Helper to make a unique ID for this specific door, in case many doors are named "Door"
    private string GetDoorID()
    {
        int x = Mathf.RoundToInt(transform.position.x * 10f);
        int y = Mathf.RoundToInt(transform.position.y * 10f);
        int z = Mathf.RoundToInt(transform.position.z * 10f);
        return gameObject.name + "_" + x + "_" + y + "_" + z;
    }

    private void SaveDoorState()
    {
        if (SaveManager.instance != null)
        {
            SaveManager.instance.UnlockDoor(GetDoorID());
        }
    }
}
