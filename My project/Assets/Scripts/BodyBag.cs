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
        
        // Bag ko foran ghaib (vanish) kar do!
        MeshRenderer bagMesh = GetComponent<MeshRenderer>();
        if (bagMesh != null) bagMesh.enabled = false;
        
        Collider bagCollider = GetComponent<Collider>();
        if (bagCollider != null) bagCollider.enabled = false;
        
        if (containsFuses)
        {
            if (fusesItem != null) fusesItem.SetActive(true);
            
            // Show a simple notification
            InteractionRaycast.Notify(playerStats, "You found the Fuses!");
        }
        else if (containsJumpscare)
        {
            if (jumpscareSound != null) AudioSource.PlayClipAtPoint(jumpscareSound, transform.position);
            if (jumpscareZombie != null) 
            {
                jumpscareZombie.SetActive(true);
                StartCoroutine(JumpscareRoutine(playerStats));
            }
            else
            {
                // Fallback agar zombie assign nahi kiya
                if (playerStats != null) playerStats.TakeDamage(5f);
            }
        }
    }

    private System.Collections.IEnumerator JumpscareRoutine(PlayerStats player)
    {
        float duration = 0.3f;
        float elapsed = 0f;
        Vector3 startPos = jumpscareZombie.transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            if (player != null && jumpscareZombie != null)
            {
                // Player ke chehre ke SAAMNE aa kar rukta hai (pehle seedha player ke andar ghus jata tha)
                Camera cam = player.GetComponentInChildren<Camera>();
                Vector3 targetPos = cam != null
                    ? cam.transform.position + cam.transform.forward * 0.9f - Vector3.up * 0.3f
                    : player.transform.position + player.transform.forward * 0.9f + Vector3.up * 1.2f;
                jumpscareZombie.transform.position = Vector3.Lerp(startPos, targetPos, t);
                Vector3 lookAt = cam != null ? cam.transform.position : player.transform.position + Vector3.up * 1.5f;
                jumpscareZombie.transform.LookAt(new Vector3(lookAt.x, jumpscareZombie.transform.position.y, lookAt.z));
            }
            yield return null;
        }

        // Hit and Damage!
        if (player != null)
        {
            player.TakeDamage(5f);
        }

        // Hit karne ke baad ghaib ho jaye (taake rasta block na ho)
        if (jumpscareZombie != null)
        {
            jumpscareZombie.SetActive(false);
        }
    }
}
