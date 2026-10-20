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
    [Tooltip("Player is se zyada door chala jaye to kaatna ruk jata hai (meters)")]
    public float cancelDistance = 3.5f;

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
    /// <summary>Read by the objective / achievement system (GameProgress): the RIGHT body has been cut open.</summary>
    public bool IsSolved => isCutOpen && isCorrectBody;
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

        // BUG FIX: pehle player 4 second ke liye jama ho jata tha jab ke enemy ko usi jagah bulaya jata tha (pakki maut).
        // Ab player hil sakta hai - lekin agar body se door gaya ya chhup gaya to kaatna beech mein ruk jayega.
        InteractionRaycast.Notify(player, "Cutting open the stomach... (stay close)", cutDuration);

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

        PlayerController pc = player.GetComponent<PlayerController>();

        // Wait while the cutting happens
        float elapsed = 0f;
        while (elapsed < cutDuration)
        {
            Collider bodyCol = GetComponent<Collider>();
            Vector3 bodyPoint = bodyCol != null ? bodyCol.ClosestPoint(player.transform.position) : transform.position;
            bool walkedAway = Vector3.Distance(player.transform.position, bodyPoint) > cancelDistance;
            bool hidden = pc != null && pc.isHidden;
            if (player.IsDead() || walkedAway || hidden)
            {
                isCutting = false;
                if (audioSource != null) audioSource.Stop();
                if (!player.IsDead()) InteractionRaycast.Notify(player, "Cutting interrupted...", 2f);
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        isCutting = false;
        isCutOpen = true;

        // BUG FIX: Disable the dead body's collider so it doesn't block you from clicking the Crank/Key!
        Collider myCollider = GetComponent<Collider>();
        if (myCollider != null) myCollider.enabled = false;

        // Spawn items if correct body
        if (isCorrectBody)
        {
            if (rustyCrank != null) rustyCrank.SetActive(true);
            if (examRoomKey != null) examRoomKey.SetActive(true);

            InteractionRaycast.Notify(player, "Found something inside!", 3f);
        }
        else
        {
            InteractionRaycast.Notify(player, "Nothing but organs here...", 3f);
        }
    }
}
