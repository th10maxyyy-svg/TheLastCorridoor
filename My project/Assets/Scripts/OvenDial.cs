using UnityEngine;

public class OvenDial : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    public int[] availableTemperatures = { 0, 100, 200, 300, 400, 500 };
    public int currentIndex = 0;
    
    [Tooltip("The visual dial object that will rotate")]
    public Transform dialTransform;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip clickSound;

    public OvenPuzzleManager puzzleManager;

    private bool isLocked = false;

    void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        
        UpdateDialVisual();
    }

    public string GetInteractPrompt()
    {
        if (isLocked) return "Oven is jammed.";
        return "Press E to Turn Dial (Current: " + GetCurrentTemp() + "°)";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isLocked) return;

        currentIndex++;
        if (currentIndex >= availableTemperatures.Length)
        {
            currentIndex = 0; // Reset to 0
        }

        UpdateDialVisual();

        if (clickSound != null) audioSource.PlayOneShot(clickSound);

        if (puzzleManager != null)
        {
            puzzleManager.CheckPuzzle();
        }
    }

    public int GetCurrentTemp()
    {
        return availableTemperatures[currentIndex];
    }

    public void LockDial()
    {
        isLocked = true;
    }

    private void UpdateDialVisual()
    {
        if (dialTransform != null)
        {
            // Simple rotation logic (dividing 360 degrees by number of options)
            float anglePerStep = 360f / availableTemperatures.Length;
            float targetAngle = currentIndex * anglePerStep;
            
            // Assuming the dial rotates on the Z axis
            dialTransform.localRotation = Quaternion.Euler(0, 0, -targetAngle);
        }
    }
}
