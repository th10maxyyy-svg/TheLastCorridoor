using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class DeadBodyPuzzle : MonoBehaviour, IInteractable
{
    [Header("Puzzle Settings")]
    [Tooltip("Check this if this is the correct body that holds the key and crank.")]
    public bool isCorrectBody = false;
    [Tooltip("How long it takes to cut open the body.")]
    public float cutDuration = 4.0f;

    [Header("Items to Spawn (If Correct)")]
    public GameObject rustyCrank;
    public GameObject examRoomKey;

    [Header("Audio Settings")]
    public AudioClip squelchingSound;
    private AudioSource audioSource;

    [Header("Enemy Attraction")]
    [Tooltip("The EnemyAI that will be alerted by the loud cutting noise.")]
    public EnemyAI enemyAI;

    private bool isCutOpen = false;
    private bool isCutting = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Hide the items initially
        if (isCorrectBody)
        {
            if (rustyCrank != null) rustyCrank.SetActive(false);
            if (examRoomKey != null) examRoomKey.SetActive(false);
        }
    }

    public string GetInteractPrompt()
    {
        if (isCutOpen) return "Already cut open.";
        if (isCutting) return "Cutting...";
        return "Press E to cut open the stomach (Needs Scalpel)";
    }

    public void Interact(PlayerStats player)
    {
        if (isCutOpen || isCutting) return;

        Inventory inventory = player.GetComponent<Inventory>();
        InteractionRaycast raycast = player.GetComponentInChildren<InteractionRaycast>();

        if (inventory != null && inventory.HasItem("Scalpel"))
        {
            StartCoroutine(CutBodyRoutine(player));
        }
        else
        {
            if (raycast != null)
            {
                raycast.ShowNotification("The skin is tough... I need something very sharp.", 4f);
            }
        }
    }

    private IEnumerator CutBodyRoutine(PlayerStats player)
    {
        isCutting = true;
        
        // Paralyze the player to build tension!
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.isCinematicPlaying = true; // Prevents movement

        // Show a message
        InteractionRaycast raycast = player.GetComponentInChildren<InteractionRaycast>();
        if (raycast != null)
        {
            raycast.ShowNotification("Cutting open the stomach...", cutDuration);
        }

        // Play the gross sound
        if (squelchingSound != null)
        {
            audioSource.PlayOneShot(squelchingSound);
        }

        // Alert the enemy IMMEDIATELY so the tension starts
        if (enemyAI != null)
        {
            // BUG FIX: Use ForceInvestigate so he searches the room for 15 seconds!
            enemyAI.ForceInvestigate(player.transform.position, 15f);
        }
        else
        {
            Debug.LogWarning("DeadBodyPuzzle: EnemyAI not assigned, enemy will not investigate!");
        }

        // Wait while the cutting happens
        yield return new WaitForSeconds(cutDuration);

        isCutting = false;
        isCutOpen = true;

        // Release the player
        if (pc != null) pc.isCinematicPlaying = false;

        // BUG FIX: Disable the dead body's collider so it doesn't block you from clicking the Crank/Key!
        Collider myCollider = GetComponent<Collider>();
        if (myCollider != null) myCollider.enabled = false;

        // Spawn items if correct body
        if (isCorrectBody)
        {
            if (rustyCrank != null) rustyCrank.SetActive(true);
            if (examRoomKey != null) examRoomKey.SetActive(true);

            if (raycast != null)
            {
                raycast.ShowNotification("Found something inside!", 3f);
            }
        }
        else
        {
            if (raycast != null)
            {
                raycast.ShowNotification("Nothing but organs here...", 3f);
            }
        }
    }
}
