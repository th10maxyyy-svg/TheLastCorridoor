using UnityEngine;
using UnityEngine.UI; // Optional UI Text support

public class InteractionRaycast : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float rayDistance = 3.0f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Optional UI References")]
    [SerializeField] private Text promptText; // Drag HUD text here if you have it

    private PlayerStats playerStats;
    private string currentPrompt = "";

    void Start()
    {
        // PlayerStats is on the parent Player object (this script is on the Camera child object)
        playerStats = GetComponentInParent<PlayerStats>();
    }

    void Update()
    {
        CheckForInteractable();
    }

    private void CheckForInteractable()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        // Perform raycast
        if (Physics.Raycast(ray, out hit, rayDistance, interactableLayer))
        {
            // Check if object has an IInteractable component
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                currentPrompt = interactable.GetInteractPrompt();

                if (Input.GetKeyDown(interactKey))
                {
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

        // Update UI if present
        if (promptText != null)
        {
            promptText.text = currentPrompt;
        }
    }

    // A simple legacy GUI overlay to display interaction prompt on screen
    // so you don't have to setup Canvas UI elements manually yet.
    void OnGUI()
    {
        if (!string.IsNullOrEmpty(currentPrompt))
        {
            // Center of screen prompt
            Rect rect = new Rect(Screen.width / 2 - 150, Screen.height / 2 + 50, 300, 40);
            
            GUIStyle style = new GUIStyle();
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 20;
            style.normal.textColor = Color.white;

            // Draw shadow first
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), currentPrompt, style);

            // Draw white text
            style.normal.textColor = Color.white;
            GUI.Label(rect, currentPrompt, style);
        }

        // Draw crosshair at the center of the screen
        Rect crosshairRect = new Rect(Screen.width / 2 - 2, Screen.height / 2 - 2, 4, 4);
        Texture2D crosshairTex = Texture2D.whiteTexture;
        GUI.color = Color.white;
        GUI.DrawTexture(crosshairRect, crosshairTex);
    }
}
