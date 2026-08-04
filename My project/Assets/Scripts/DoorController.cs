using UnityEngine;

public class DoorController : MonoBehaviour, IInteractable
{
    [Header("Lock Settings")]
    [SerializeField] private bool isLocked = false;
    [SerializeField] private string requiredKey = "RedKey";

    [Header("Movement Settings")]
    [SerializeField] private float openAngle = 90.0f;
    [SerializeField] private float smoothSpeed = 3.0f;

    private bool isOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    void Start()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
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
                isOpen = true;
                Debug.Log("Door unlocked and opened!");
            }
            else
            {
                Debug.Log("Door is locked!");
            }
        }
        else
        {
            isOpen = !isOpen;
        }
    }
}
