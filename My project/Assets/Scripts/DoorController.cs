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
    }

    void Update()
    {
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * smoothSpeed);
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
            Inventory inventory = playerStats.GetComponent<Inventory>();
            if (inventory != null && inventory.HasItem(requiredKey))
            {
                isLocked = false;
                ToggleDoor();
                Debug.Log("Door unlocked and opened!");
            }
            else
            {
                Debug.Log("Door is locked!");
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
}
