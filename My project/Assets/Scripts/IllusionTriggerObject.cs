using UnityEngine;
using UnityEngine.Events;


public class IllusionTriggerObject : MonoBehaviour, IInteractable
{
    public string interactPrompt = "Press E to Inspect";
    public UnityEvent onInteract = new UnityEvent();
    
    private bool hasInteracted = false;

    public string GetInteractPrompt()
    {
        if (hasInteracted) return "";
        return interactPrompt;
    }

    public void Interact(PlayerStats playerStats)
    {
        if (hasInteracted) return;
        hasInteracted = true;
        
        if (onInteract != null) onInteract.Invoke();
        
        // Agar is par rigidbody hai to ye gir jayega
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
    }
}
