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

        if (inventory.HasItem(requiredValveItem))
        {
            isSolved = true;
            inventory.RemoveItem(requiredValveItem); // Hath se nikal lo
            
            // Pipe par laga hua valve show kar do
            if (valveMesh != null) valveMesh.gameObject.SetActive(true);
            
            StartCoroutine(DrainRoutine());
        }
        else
        {
            // Agar Valve nahi hai to Hint do
            InteractionRaycast.Notify(playerStats, "The acid needs to be drained... I need a Valve handle.");
        }
    }

    private IEnumerator DrainRoutine()
    {
        // 1. Valve ghumane ki awaz
        if (valveTurnSound != null && audioSource != null) 
        {
            audioSource.PlayOneShot(valveTurnSound);
        }
        
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
        if (acidDrainSound != null && audioSource != null) 
        {
            audioSource.PlayOneShot(acidDrainSound);
        }

        // 3. Tezaab ko aahista aahista neechay le jao (drain karo)
        if (acidMesh != null)
        {
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
        }
        else
        {
            Debug.LogError("AcidMesh is null inside DrainRoutine! Cannot move acid.");
        }

        // 4. Sab complete hone par Key ko accessible banao!
        if (onDrainComplete != null)
        {
            onDrainComplete.Invoke();
        }
    }
}
