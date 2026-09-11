using UnityEngine;

public class BodyBag : MonoBehaviour, IInteractable
{
    [Header("Bag Contents")]
    [Tooltip("Tick this if this bag contains the real item")]
    public bool containsFuses = false;
    [Tooltip("Tick this if opening this bag triggers a jumpscare")]
    public bool containsJumpscare = false;
    
    [Header("References")]
    public GameObject fusesItem; // Andar rakha hua item (Fuse Box)
    public GameObject jumpscareZombie; // Andar rakha hua zombie/ghost model
    public Animator bagAnimator;
    
    [Header("Audio")]
    public AudioClip unzipSound;
    public AudioClip jumpscareSound;
    
    private bool isOpened = false;

    void Start()
    {
        if (fusesItem != null) fusesItem.SetActive(false);
        if (jumpscareZombie != null) jumpscareZombie.SetActive(false);
    }
    
    public string GetInteractPrompt()
    {
        return isOpened ? "" : "Press E to Unzip Body Bag";
    }
    
    public void Interact(PlayerStats playerStats)
    {
        if (isOpened) return;
        isOpened = true;
        
        if (unzipSound != null) AudioSource.PlayClipAtPoint(unzipSound, transform.position);
        if (bagAnimator != null) bagAnimator.SetTrigger("Unzip"); // Agar zip khulne ki animation hai
        
        if (containsFuses)
        {
            if (fusesItem != null) fusesItem.SetActive(true);
            
            // Show a simple notification
            InteractionRaycast ir = Camera.main.GetComponent<InteractionRaycast>();
            if (ir != null) ir.ShowNotification("You found the Fuses!");
        }
        else if (containsJumpscare)
        {
            if (jumpscareSound != null) AudioSource.PlayClipAtPoint(jumpscareSound, transform.position);
            if (jumpscareZombie != null) jumpscareZombie.SetActive(true);
            
            // Jumpscare punishment
            if (playerStats != null)
            {
                playerStats.TakeDamage(15f); // Thora damage de do
            }
        }
    }
}
