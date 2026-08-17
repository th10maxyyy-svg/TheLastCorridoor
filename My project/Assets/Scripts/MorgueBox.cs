using UnityEngine;
using System.Collections;

public class MorgueBox : MonoBehaviour, IInteractable
{
    [Header("Drawer Settings")]
    [Tooltip("The part of the morgue box that slides out (the drawer)")]
    public Transform slidingDrawer;
    
    [Tooltip("How much and in which local direction the drawer slides. Try (0, 0, 1.5) or (1.5, 0, 0) based on your model")]
    public Vector3 slideOffset = new Vector3(0, 0, 1.5f);
    public float slideSpeed = 2f;
    
    [Header("Events & Contents")]
    public bool containsKey = false;
    [Tooltip("Place the Key GameObject here. It will be hidden until the drawer is opened.")]
    public GameObject hiddenKeyObject;
    
    [Space(10)]
    public bool isJumpScare = false;
    public AudioSource audioSource;
    public AudioClip scareSound;

    private bool isOpen = false;
    private bool isMoving = false;
    private Vector3 closedPosition;

    void Start()
    {
        if (slidingDrawer != null)
        {
            closedPosition = slidingDrawer.localPosition;
        }
        
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
        if (isMoving || slidingDrawer == null) return;
        
        StartCoroutine(SlideDrawer(!isOpen));
    }

    private IEnumerator SlideDrawer(bool opening)
    {
        isMoving = true;
        
        Vector3 targetPos = opening ? closedPosition + slideOffset : closedPosition;
        Vector3 startPos = slidingDrawer.localPosition;
        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime * slideSpeed;
            slidingDrawer.localPosition = Vector3.Lerp(startPos, targetPos, t);
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
        }
    }
}
