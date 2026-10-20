using UnityEngine;
using System.Collections;

public class MorgueBox : MonoBehaviour, IInteractable
{
    [Header("Morgue Box Parts")]
    [Tooltip("The door that swings open")]
    public Transform swingingDoor;
    public Vector3 doorOpenRotation = new Vector3(0, 90, 0);

    [Tooltip("The tray/bed that slides out")]
    public Transform slidingTray;
    public Vector3 slideOffset = new Vector3(0, 0, 1.5f);
    
    public float openSpeed = 2f;
    
    [Header("Events & Contents")]
    public bool containsKey = false;
    [Tooltip("Place the Key GameObject here. It will be hidden until the drawer is opened.")]
    public GameObject hiddenKeyObject;
    
    [Space(10)]
    public bool isJumpScare = false;
    public AudioSource audioSource;
    public AudioClip scareSound;

    private bool isOpen = false;
    private bool wasJumpScare = false;

    /// <summary>An empty drawer (no key, no scare) holds a toe tag with hidden lore (FR_64).</summary>
    public bool HoldsLore => !containsKey && !wasJumpScare;
    private bool isMoving = false;
    private Vector3 trayClosedPosition;
    private Quaternion doorClosedRotation;

    void Start()
    {
        wasJumpScare = isJumpScare;
        if (slidingTray != null) trayClosedPosition = slidingTray.localPosition;
        if (swingingDoor != null) doorClosedRotation = swingingDoor.localRotation;
        
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        
        if (containsKey && hiddenKeyObject != null)
        {
            hiddenKeyObject.SetActive(false); // Hide the key initially
        }
    }

    public string GetInteractPrompt()
    {
        if (isMoving) return "";
        return isOpen ? "Press E to close drawer" : "Press E to open Morgue Box";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isMoving) return;
        
        StartCoroutine(OpenMorgue(!isOpen, playerStats));
    }

    private IEnumerator OpenMorgue(bool opening, PlayerStats playerStats)
    {
        isMoving = true;
        
        float currentSpeed = (isJumpScare && opening) ? openSpeed * 5f : openSpeed;

        Vector3 trayTargetPos = opening && slidingTray != null ? trayClosedPosition + slideOffset : trayClosedPosition;
        Quaternion doorTargetRot = opening && swingingDoor != null ? doorClosedRotation * Quaternion.Euler(doorOpenRotation) : doorClosedRotation;
        
        Vector3 trayStartPos = slidingTray != null ? slidingTray.localPosition : Vector3.zero;
        Quaternion doorStartRot = swingingDoor != null ? swingingDoor.localRotation : Quaternion.identity;

        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * currentSpeed;
            if (slidingTray != null) slidingTray.localPosition = Vector3.Lerp(trayStartPos, trayTargetPos, t);
            if (swingingDoor != null) swingingDoor.localRotation = Quaternion.Lerp(doorStartRot, doorTargetRot, t);
            yield return null;
        }

        isOpen = opening;
        isMoving = false;

        // Trigger events only when fully opened
        if (isOpen)
        {
            if (containsKey && hiddenKeyObject != null)
            {
                hiddenKeyObject.SetActive(true); // Reveal the key
            }
            
            if (isJumpScare)
            {
                if (audioSource != null && scareSound != null)
                {
                    audioSource.PlayOneShot(scareSound);
                }
                
                // P.S: You can also spawn a blood particle effect or play a scary animation here!
                
                // Only scare once!
                isJumpScare = false;
            }

            // HIDDEN LORE: empty drawers hold a toe tag (shown once, then kept in the journal)
            if (HoldsLore) GameProgress.LoreFound(this, playerStats);
        }
    }
}
