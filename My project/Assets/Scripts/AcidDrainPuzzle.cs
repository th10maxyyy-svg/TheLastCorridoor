using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class AcidDrainPuzzle : MonoBehaviour, IInteractable
{
    [Header("Puzzle Settings")]
    public string requiredValveItem = "Valve";
    
    [Header("Visuals to Move")]
    [Tooltip("Pani/Tezaab ka 3D model (mesh) jisay neechay karna hai")]
    public Transform acidMesh; 
    [Tooltip("Valve ka model jo puzzle complete hone par yahan pipe par show hoga")]
    public Transform valveMesh; 
    
    [Header("Animation Settings")]
    public float drainDuration = 3f;
    [Tooltip("Tezaab kitna neechay jayega (Misaal ke tor par -1.5 likhne se wo 1.5 meter neechay chala jayega)")]
    public float drainAmountY = -1.5f; 
    
    [Header("Audio")]
    public AudioClip valveTurnSound;
    public AudioClip acidDrainSound;
    private AudioSource audioSource;
    
    [Header("Events")]
    [Tooltip("Jab tezaab drain ho jaye to kya ho? (Yahan Key ka collider ON kar sakte hain)")]
    public UnityEvent onDrainComplete; 

    private bool isSolved = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        
        // Shuru mein pipe par valve nahi hona chahiye
        if (valveMesh != null) valveMesh.gameObject.SetActive(false);
    }

    public string GetInteractPrompt()
    {
        if (isSolved) return "";
        return "Press E to Attach Valve";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isSolved) return;

        Inventory inventory = playerStats.GetComponent<Inventory>();
        if (inventory == null)
        {
            Debug.LogError("PlayerStats does not have an Inventory component!");
            return;
        }

        Debug.Log("Player is interacting with Acid Drain. Required item: " + requiredValveItem);
        Debug.Log("Is holding an item? " + inventory.HasItem());
        if (inventory.HasItem())
        {
            Debug.Log("Currently holding item name: " + inventory.GetCurrentItem().name);
            KeyItem heldKey = inventory.GetCurrentItem().GetComponent<KeyItem>();
            if (heldKey != null)
            {
                Debug.Log("Held item KeyName is: '" + heldKey.GetKeyName() + "'");
            }
            else
            {
                Debug.Log("Held item does not have a KeyItem script.");
            }
        }

        if (inventory.HasItem(requiredValveItem))
        {
            Debug.Log("SUCCESS! Valve found in inventory. Draining acid...");
            isSolved = true;
            inventory.RemoveItem(requiredValveItem); // Hath se nikal lo
            
            // Pipe par laga hua valve show kar do
            if (valveMesh != null) valveMesh.gameObject.SetActive(true);
            
            StartCoroutine(DrainRoutine());
        }
        else
        {
            Debug.LogWarning("FAILED: Player does not have the exact required item: '" + requiredValveItem + "'");
            // Agar Valve nahi hai to Hint do
            InteractionRaycast ir = Camera.main.GetComponent<InteractionRaycast>();
            if (ir != null) ir.ShowNotification("The acid needs to be drained... I need a Valve handle.");
        }
    }

    private IEnumerator DrainRoutine()
    {
        Debug.Log("DrainRoutine Started!");
        // 1. Valve ghumane ki awaz
        if (valveTurnSound != null && audioSource != null) 
        {
            audioSource.PlayOneShot(valveTurnSound);
            Debug.Log("Played valve sound.");
        }
        
        Debug.Log("Waiting for 1 second (valve turn delay)...");
        // Valve ghumao (1 second ke liye)
        float elapsed = 0f;
        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime;
            if (valveMesh != null)
            {
                // Z-axis par rotate
                valveMesh.Rotate(Vector3.forward * Time.deltaTime * 360f);
            }
            yield return null;
        }

        Debug.Log("Valve delay finished! Now playing drain sound.");
        // 2. Tezaab ke neechay jane ki awaz
        if (acidDrainSound != null && audioSource != null) 
        {
            audioSource.PlayOneShot(acidDrainSound);
        }

        // 3. Tezaab ko aahista aahista neechay le jao (drain karo)
        if (acidMesh != null)
        {
            Debug.Log("AcidMesh found! Starting to lerp down by " + drainAmountY);
            Vector3 startPos = acidMesh.position;
            Vector3 targetPos = startPos + new Vector3(0, drainAmountY, 0); // Neechay ki taraf offset
            
            elapsed = 0f;
            while (elapsed < drainDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / drainDuration;
                acidMesh.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }
            acidMesh.position = targetPos; // Make sure it reaches exact target
            Debug.Log("Acid Lerp finished! Final position: " + acidMesh.position);
        }
        else
        {
            Debug.LogError("AcidMesh is null inside DrainRoutine! Cannot move acid.");
        }

        // 4. Sab complete hone par Key ko accessible banao!
        if (onDrainComplete != null)
        {
            Debug.Log("Invoking onDrainComplete event.");
            onDrainComplete.Invoke();
        }
        Debug.Log("DrainRoutine completely finished!");
    }
}
