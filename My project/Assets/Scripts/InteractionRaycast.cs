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
    private string notificationMessage = "";
    private float notificationTimer = 0f;

    private GUIStyle notifStyle;
    private GUIStyle promptStyle;

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
            return;
        }

        if (playerController != null)
        {
            // No interacting while dead or during a cinematic
            if (playerController.isDead || playerController.isCinematicPlaying)
            {
                currentPrompt = "";
                return;
            }

            if (playerController.isHidden)
            {
                currentPrompt = "Press E to Exit";
                return;
            }
        }

        IInteractable interactable = FindInteractable();
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
    private IInteractable FindInteractable()
    {
        RaycastHit[] hits = Physics.RaycastAll(new Ray(transform.position, transform.forward), rayDistance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Transform playerRoot = playerStats != null ? playerStats.transform : null;

        foreach (RaycastHit hit in hits)
        {
            if (playerRoot != null && hit.transform.IsChildOf(playerRoot)) continue;

            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null) return interactable;

            // A solid wall/object blocks everything behind it; a plain trigger volume doesn't
            if (!hit.collider.isTrigger) return null;
        }
        return null;
    }

    void OnGUI()
    {
        if (notifStyle == null)
        {
            notifStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = true };
            promptStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
        }

        // Notification Text
        if (!string.IsNullOrEmpty(notificationMessage))
        {
            notifStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(Screen.width / 2 - 299, Screen.height / 2 - 169, 600, 80), notificationMessage, notifStyle);

            notifStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(Screen.width / 2 - 300, Screen.height / 2 - 170, 600, 80), notificationMessage, notifStyle);
        }

        // Interaction Prompt text
        if (!string.IsNullOrEmpty(currentPrompt))
        {
            // Shadow effect
            promptStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(Screen.width / 2 - 249, Screen.height / 2 + 51, 500, 40), currentPrompt, promptStyle);

            // Main white text
            promptStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width / 2 - 250, Screen.height / 2 + 50, 500, 40), currentPrompt, promptStyle);
        }

        // Center crosshair dot (hidden while a keypad is open)
        if (KeypadController.IsAnyKeypadOpen) return;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(Screen.width / 2 - 2, Screen.height / 2 - 2, 5, 5), Texture2D.whiteTexture);
    }
}
