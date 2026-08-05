using UnityEngine;

public class InteractionRaycast : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float rayDistance = 3.0f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private PlayerStats playerStats;
    private string currentPrompt = "";

    void Start()
    {
        playerStats = GetComponentInParent<PlayerStats>();
    }

    void Update()
    {
        CheckForInteractable();
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
