using UnityEngine;

public class InteractionRaycast : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float rayDistance = 3.0f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [Header("Audio Settings")]
    [SerializeField] private AudioSource interactionAudioSource;
    [SerializeField] private AudioClip pickupSound;

    private PlayerStats playerStats;
    private PlayerController playerController;
    private string currentPrompt = "";
    private string currentRoom = "";
    private string currentFloor = "";
    [SerializeField] private float roomNameDistance = 7f;
    private string notificationMessage = "";
    private float notificationTimer = 0f;

    public void ShowNotification(string message, float duration = 3f)
    {
        notificationMessage = message;
        notificationTimer = duration;
    }

    /// <summary>Shows a message on the player's screen. Safe to call even if Camera.main is missing.</summary>
    public static void Notify(Component player, string message, float duration = 3f)
    {
        InteractionRaycast ir = player != null ? player.GetComponentInChildren<InteractionRaycast>() : null;
        if (ir == null && Camera.main != null) ir = Camera.main.GetComponent<InteractionRaycast>();
        if (ir == null) ir = FindAnyObjectByType<InteractionRaycast>();
        if (ir != null) ir.ShowNotification(message, duration);
    }

    void Start()
    {
        TLCCanvas.Ensure();
        playerStats = GetComponentInParent<PlayerStats>();
        if (playerStats != null) playerController = playerStats.GetComponent<PlayerController>();
    }

    void Update()
    {
        CheckForInteractable();

        if (notificationTimer > 0)
        {
            notificationTimer -= Time.deltaTime;
            if (notificationTimer <= 0)
            {
                notificationMessage = "";
            }
        }
    }

    private void CheckForInteractable()
    {
        if (KeypadController.IsAnyKeypadOpen)
        {
            currentPrompt = ""; // the keypad panel is on screen - no "Press E" text over it
            currentRoom = "";
            return;
        }

        if (playerController != null)
        {
            // No interacting while dead or during a cinematic
            if (playerController.isDead || playerController.isCinematicPlaying)
            {
                currentPrompt = "";
                currentRoom = "";
                return;
            }

            if (playerController.isHidden)
            {
                currentPrompt = "Press E to Exit";
                return;
            }
        }

        // ROOM NAME: looking at a room door (also from a few metres away) shows which room it is
        DoorController lookedDoor = FindInteractable(roomNameDistance) as DoorController;
        currentRoom = lookedDoor != null ? lookedDoor.RoomName : "";
        currentFloor = lookedDoor != null ? lookedDoor.FloorName : "";

        IInteractable interactable = FindInteractable(rayDistance);
        if (interactable == null)
        {
            currentPrompt = "";
            return;
        }

        currentPrompt = interactable.GetInteractPrompt();

        if (Input.GetKeyDown(interactKey))
        {
            if (interactionAudioSource != null && pickupSound != null)
            {
                interactionAudioSource.PlayOneShot(pickupSound);
            }
            interactable.Interact(playerStats);
        }
    }

    /// <summary>
    /// Nearest interactable in front of the camera. Invisible trigger volumes don't block the ray any more,
    /// the player's own colliders are skipped, and clicking a child mesh still finds the script on its parent.
    /// </summary>
    private IInteractable FindInteractable(float distance)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Ray(transform.position, transform.forward), distance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Transform playerRoot = playerStats != null ? playerStats.transform : null;

        foreach (RaycastHit hit in hits)
        {
            if (playerRoot != null && hit.transform.IsChildOf(playerRoot)) continue;

            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null) return interactable;

            // A solid wall/object blocks everything behind it; a plain trigger volume doesn't.
            // Exception: a small item lying ON that surface (scalpel on a shelf) whose box starts just behind the hit point.
            if (!hit.collider.isTrigger)
            {
                foreach (RaycastHit h2 in hits)
                {
                    if (h2.distance <= hit.distance || h2.distance > hit.distance + 0.2f) continue;
                    KeyItem item = h2.collider.GetComponentInParent<KeyItem>();
                    if (item != null) return item;
                }
                return null;
            }
        }
        return null;
    }

    /// <summary>The HUD texts are real objects on the TLC_UI canvas (HUD/RoomName, HUD/InteractPrompt, ...); this fills them in.</summary>
    void LateUpdate()
    {
        TLCCanvas ui = TLCCanvas.Instance;
        if (ui == null) return;
        bool blocked = playerController != null && (playerController.isDead || playerController.isCinematicPlaying);
        TLCCanvas.SetText(ui.notification, notificationMessage);
        TLCCanvas.SetText(ui.roomName, string.IsNullOrEmpty(currentRoom) ? "" : currentRoom.ToUpper());
        TLCCanvas.SetText(ui.floorName, string.IsNullOrEmpty(currentRoom) || string.IsNullOrEmpty(currentFloor) ? "" : currentFloor.ToUpper());
        TLCCanvas.SetText(ui.prompt, currentPrompt);
        if (ui.crosshair != null) ui.crosshair.enabled = !KeypadController.IsAnyKeypadOpen && !blocked;
    }

    void OnDisable()
    {
        TLCCanvas ui = TLCCanvas.Instance;
        if (ui == null) return;
        TLCCanvas.SetText(ui.notification, ""); TLCCanvas.SetText(ui.roomName, ""); TLCCanvas.SetText(ui.floorName, ""); TLCCanvas.SetText(ui.prompt, "");
        if (ui.crosshair != null) ui.crosshair.enabled = false;
    }
}
