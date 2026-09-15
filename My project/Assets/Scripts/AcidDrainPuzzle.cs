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
    [Tooltip("Tezaab kitna neechay jayega (Y axis mein)")]
    public float targetAcidYPos = -1.5f; 
    
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
        // 1. Valve ghumane ki awaz
        if (valveTurnSound != null) audioSource.PlayOneShot(valveTurnSound);
        
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

        // 2. Tezaab ke neechay jane ki awaz
        if (acidDrainSound != null) audioSource.PlayOneShot(acidDrainSound);

        // 3. Tezaab ko aahista aahista neechay le jao (drain karo)
        if (acidMesh != null)
        {
            Vector3 startPos = acidMesh.localPosition;
            Vector3 targetPos = new Vector3(startPos.x, targetAcidYPos, startPos.z);
            
            elapsed = 0f;
            while (elapsed < drainDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / drainDuration;
                acidMesh.localPosition = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }
        }

        // 4. Sab complete hone par Key ko accessible banao!
        if (onDrainComplete != null)
        {
            onDrainComplete.Invoke();
        }
    }
}
