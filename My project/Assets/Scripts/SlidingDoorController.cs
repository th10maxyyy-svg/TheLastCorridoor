using UnityEngine;

public class SlidingDoorController : MonoBehaviour, IInteractable
{
    [Header("Movement Settings")]
    [SerializeField] private Vector3 slideDirection = Vector3.forward; // Direction to slide
    [SerializeField] private float slideDistance = 1.5f;
    [SerializeField] private float smoothSpeed = 5.0f;

    [Header("Lock Settings")]
    [SerializeField] private bool isLocked = false;
    [SerializeField] private string requiredKey = "";

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onDoorUnlocked;

    private bool isOpen = false;
    private Vector3 closedPosition;
    private Vector3 openPosition;

    void Start()
    {
        closedPosition = transform.localPosition;
        openPosition = closedPosition + (slideDirection.normalized * slideDistance);

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
            }
        }
    }

    void Update()
    {
        Vector3 targetPosition = isOpen ? openPosition : closedPosition;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * smoothSpeed);
    }

    public string GetInteractPrompt()
    {
        if (isLocked)
            return "Door is Locked.";
            
        return isOpen ? "Press E to Close Door" : "Press E to Open Door";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isLocked)
        {
            Inventory inventory = playerStats.GetComponent<Inventory>();
            if (inventory != null && inventory.HasItem(requiredKey))
            {
                isLocked = false;
                ToggleDoor();
                onDoorUnlocked?.Invoke();
                Debug.Log("Sliding Door unlocked and opened!");

                // NAYA FIX: Auto-Trigger Cinematic Ambush! (Bina Unity Editor mein set kiye)
                Debug.Log("Checking required key for Ambush: " + requiredKey);
                if (requiredKey.ToLower().Contains("guard"))
                {
                    CinematicAmbush[] ambushes = FindObjectsByType<CinematicAmbush>(FindObjectsSortMode.None);
                    Debug.Log("Found " + ambushes.Length + " CinematicAmbushes in the scene.");
                    foreach (var ambush in ambushes)
                    {
                        ambush.TriggerAmbush();
                    }
                }
            }
            else
            {
                Debug.Log("Sliding Door is locked!");
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

        if (audioSource != null)
        {
            if (isOpen && openSound != null)
                audioSource.PlayOneShot(openSound);
            else if (!isOpen && closeSound != null)
                audioSource.PlayOneShot(closeSound);
        }
    }
    
    // Method for Elevator to automatically open/close the doors
    public void SetOpenState(bool state)
    {
        if (isOpen != state)
        {
            ToggleDoor();
        }
    }
}
