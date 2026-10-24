using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class FuseBoxPuzzle : MonoBehaviour, IInteractable
{
    [Header("Puzzle Settings")]
    public int requiredFuses = 2;
    private int insertedFuses = 0;
    
    [Tooltip("The name of the fuse item in the player's inventory")]
    public string fuseItemName = "Fuse";

    [Header("Timer Settings")]
    public AudioClip beepSound;

    [Header("Rewards & Events")]
    [Tooltip("Fires when the countdown finishes")]
    public UnityEvent onPuzzleComplete;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip insertFuseSound;
    public AudioClip boxOpenSound;
    public AudioClip errorSound;

    private bool isComplete = false;
    /// <summary>Read by the objective / achievement system (GameProgress).</summary>
    public bool IsSolved => isComplete;

    private string SaveFlag => "FuseBoxDone_" + SaveManager.GetSceneObjectId(transform);

    void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // BUG FIX: a finished fuse box used to reset to 0 fuses after a death / load (objective and prompt went back)
        if (SaveManager.instance != null && SaveManager.instance.HasFlag(SaveFlag))
        {
            isComplete = true;
            insertedFuses = requiredFuses;
            onPuzzleComplete?.Invoke();
        }
        else if (SaveManager.instance != null)
        {
            // fuses already put in stay in the box (they are used up and never lie around again)
            while (insertedFuses < requiredFuses && SaveManager.instance.HasFlag(SaveFlag + "_" + (insertedFuses + 1))) insertedFuses++;
        }
    }

    public string GetInteractPrompt()
    {
        if (isComplete) return "System Restored.";
        
        return "Press E to insert Fuse (" + insertedFuses + "/" + requiredFuses + ")";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isComplete) return;

        Inventory inventory = playerStats.GetComponent<Inventory>();
        if (inventory != null && inventory.HasItem(fuseItemName))
        {
            // Insert one fuse
            insertedFuses++;
            inventory.RemoveItem(fuseItemName, false);   // USED UP: the fuse stays in the box
            if (SaveManager.instance != null) SaveManager.instance.SetFlag(SaveFlag + "_" + insertedFuses);
            
            if (insertFuseSound != null) audioSource.PlayOneShot(insertFuseSound);

            if (insertedFuses >= requiredFuses)
            {
                isComplete = true; // Taake aur interact na ho
                if (SaveManager.instance != null) SaveManager.instance.SetFlag(SaveFlag);
                
                InteractionRaycast raycast = playerStats.GetComponentInChildren<InteractionRaycast>();
                StartCoroutine(CountdownRoutine(raycast));
            }
        }
        else
        {
            if (errorSound != null) audioSource.PlayOneShot(errorSound);
        }
    }

    private IEnumerator CountdownRoutine(InteractionRaycast raycast)
    {
        // 4 Seconds ka timer
        for (int i = 4; i > 0; i--)
        {
            if (raycast != null)
            {
                raycast.ShowNotification("SYSTEM REBOOTING... " + i, 1f);
            }
            
            if (beepSound != null) audioSource.PlayOneShot(beepSound);
            
            yield return new WaitForSeconds(1f);
        }

        // Timer end
        if (raycast != null)
        {
            raycast.ShowNotification("SYSTEM ONLINE - DOOR OPENED", 3f);
        }

        if (boxOpenSound != null) audioSource.PlayOneShot(boxOpenSound);

        if (onPuzzleComplete != null)
        {
            onPuzzleComplete.Invoke();
        }
    }
}
