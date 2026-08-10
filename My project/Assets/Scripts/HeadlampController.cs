using UnityEngine;

public class HeadlampController : MonoBehaviour
{
    [Header("Headlamp Settings")]
    [SerializeField] private Light headlampLight; // Drag the Spotlight here
    [SerializeField] private KeyCode toggleKey = KeyCode.F;
    [SerializeField] private bool hasHeadlamp = false; // Will be true when picked up
    [SerializeField] private bool isOn = false;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clickSound; // Sound when pressing F

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Start with light off
        if (headlampLight != null)
        {
            headlampLight.enabled = isOn;
        }
    }

    void Update()
    {
        // Only allow toggle if player has picked it up
        if (hasHeadlamp && Input.GetKeyDown(toggleKey))
        {
            ToggleHeadlamp();
        }
    }

    private void ToggleHeadlamp()
    {
        isOn = !isOn;
        
        if (headlampLight != null)
        {
            headlampLight.enabled = isOn;
        }

        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }
    }

    // Called by the Pickup script
    public void PickupHeadlamp()
    {
        hasHeadlamp = true;
        
        // Optionally turn it on immediately when picked up
        if (!isOn)
        {
            ToggleHeadlamp();
        }
        
        Debug.Log("Headlamp Acquired! Press F to toggle.");
    }
}
