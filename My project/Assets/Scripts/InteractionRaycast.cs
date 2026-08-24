using UnityEngine;

public class InteractionRaycast : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float rayDistance = 3.0f;
    [Header("Audio Settings")]
    [SerializeField] private AudioSource interactionAudioSource;
    [SerializeField] private AudioClip pickupSound;

    private PlayerStats playerStats;
    private string currentPrompt = "";
    private string notificationMessage = "";
    private float notificationTimer = 0f;

    public void ShowNotification(string message, float duration = 3f)
    {
        notificationMessage = message;
        notificationTimer = duration;
    }

    void Start()
    {
        playerStats = GetComponentInParent<PlayerStats>();
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
        if (playerStats != null)
        {
            PlayerController pc = playerStats.GetComponent<PlayerController>();
            if (pc != null && pc.isHidden)
            {
                currentPrompt = "Press E to Exit";
                return;
            }
        }

        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        // LayerMask parameter hataya gaya - ab sab objects detect honge
        if (Physics.Raycast(ray, out hit, rayDistance))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
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
            else
            {
                currentPrompt = "";
            }
        }
        else
        {
            currentPrompt = "";
        }
    }

    void OnGUI()
    {
        // Notification Text
        if (!string.IsNullOrEmpty(notificationMessage))
        {
            GUIStyle notifStyle = new GUIStyle();
            notifStyle.alignment = TextAnchor.MiddleCenter;
            notifStyle.fontSize = 24;
            notifStyle.fontStyle = FontStyle.Bold;

            notifStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(Screen.width / 2 - 199, Screen.height / 2 - 149, 400, 40), notificationMessage, notifStyle);

            notifStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(Screen.width / 2 - 200, Screen.height / 2 - 150, 400, 40), notificationMessage, notifStyle);
        }

        // Interaction Prompt text
        if (!string.IsNullOrEmpty(currentPrompt))
        {
            GUIStyle style = new GUIStyle();
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 22;
            style.fontStyle = FontStyle.Bold;

            // Shadow effect
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(Screen.width / 2 - 149, Screen.height / 2 + 51, 300, 40), currentPrompt, style);

            // Main white text
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 + 50, 300, 40), currentPrompt, style);
        }

        // Center crosshair dot
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(Screen.width / 2 - 2, Screen.height / 2 - 2, 5, 5), Texture2D.whiteTexture);
    }
}
