using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class MicrowavePuzzle : MonoBehaviour, IInteractable
{
    [Header("Puzzle Settings")]
    [SerializeField] private string requiredItemName = "IceBlock";
    [SerializeField] private float meltTime = 5f;
    [SerializeField] private GameObject hiddenKeyObject; // The key that spawns after melting

    [Header("Audio Effects")]
    [SerializeField] private AudioClip microwaveHumSound;
    [SerializeField] private AudioClip dingSound;

    [Header("Events")]
    public UnityEvent OnPuzzleComplete;

    private bool isMicrowaveRunning = false;
    private bool isPuzzleCompleted = false;
    /// <summary>Read by the objective / achievement system (GameProgress).</summary>
    public bool IsSolved => isPuzzleCompleted;
    private AudioSource audioSource;
    private string SaveFlag => "MicrowaveDone_" + SaveManager.GetSceneObjectId(transform);

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (SaveManager.instance != null && SaveManager.instance.HasFlag(SaveFlag))
        {
            isPuzzleCompleted = true;
            if (hiddenKeyObject != null) hiddenKeyObject.SetActive(true);
            return;
        }
        
        // Ensure the key is hidden at the start
        if (hiddenKeyObject != null)
        {
            hiddenKeyObject.SetActive(false);
        }
    }

    public string GetInteractPrompt()
    {
        if (isPuzzleCompleted) return "";
        if (isMicrowaveRunning) return "Microwave is running...";
        return "Press E to Use Microwave";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isPuzzleCompleted || isMicrowaveRunning) return;

        Inventory inventory = playerStats.GetComponent<Inventory>();
        if (inventory != null && inventory.HasItem(requiredItemName))
        {
            // Player has the ice block!
            inventory.RemoveItem(requiredItemName, false); // USED UP: the ice melted - it never comes back
            StartCoroutine(RunMicrowaveSequence());
        }
        else
        {
            // Optional message if they don't have the item
            InteractionRaycast raycast = playerStats.GetComponentInChildren<InteractionRaycast>();
            if (raycast != null)
            {
                raycast.ShowNotification("I need something frozen to melt in here...", 3f);
            }
        }
    }

    private IEnumerator RunMicrowaveSequence()
    {
        isMicrowaveRunning = true;
        Debug.Log("Microwave Started!");

        if (microwaveHumSound != null)
        {
            audioSource.clip = microwaveHumSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Wait for melting to finish
        yield return new WaitForSeconds(meltTime);

        // Ding!
        if (audioSource != null) audioSource.Stop();
        if (dingSound != null) audioSource.PlayOneShot(dingSound);

        Debug.Log("Microwave Finished!");

        // Reveal the Key!
        if (hiddenKeyObject != null)
        {
            hiddenKeyObject.SetActive(true);
        }

        isPuzzleCompleted = true;
        isMicrowaveRunning = false;
        if (SaveManager.instance != null) SaveManager.instance.SetFlag(SaveFlag);

        // Trigger any final horror events (like Enemy Ambush!)
        OnPuzzleComplete?.Invoke();
    }
}
