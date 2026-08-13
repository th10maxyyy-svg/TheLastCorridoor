using UnityEngine;

public class ReadableNote : MonoBehaviour, IInteractable
{
    [Header("Note Content")]
    [TextArea(5, 10)]
    [SerializeField] private string noteText = "Write your mystery text here...";
    
    [Header("UI Appearance")]
    [Tooltip("Download an old paper image from Google and place it here")]
    [SerializeField] private Texture2D paperTexture;
    
    private bool isReading = false;

    public string GetInteractPrompt()
    {
        return "Press E to read note";
    }

    public void Interact(PlayerStats playerStats)
    {
        isReading = !isReading;
        
        if (isReading)
        {
            // Lock player movement
            PlayerController pc = playerStats.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = false;
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            CloseNote(playerStats.gameObject);
        }
    }
    
    private void CloseNote(GameObject player)
    {
        isReading = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = true;
    }

    private void Update()
    {
        if (!isReading) return;
        
        // Allow exiting with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            PlayerStats stats = FindAnyObjectByType<PlayerStats>();
            if (stats != null) CloseNote(stats.gameObject);
        }
    }

    private void OnGUI()
    {
        if (!isReading) return;

        // Draw a simple 2D Paper in the center of the screen
        int boxWidth = 500;
        int boxHeight = 400;
        float startX = (Screen.width - boxWidth) / 2;
        float startY = (Screen.height - boxHeight) / 2;

        GUIStyle paperStyle = new GUIStyle(GUI.skin.box);
        paperStyle.normal.background = Texture2D.whiteTexture; // Basic white paper look (if no texture)

        if (paperTexture != null)
        {
            GUI.DrawTexture(new Rect(startX, startY, boxWidth, boxHeight), paperTexture, ScaleMode.StretchToFill);
        }
        else
        {
            GUI.Box(new Rect(startX, startY, boxWidth, boxHeight), "", paperStyle);
        }

        // Draw Text (Black ink)
        GUIStyle textStyle = new GUIStyle();
        textStyle.fontSize = 26;
        textStyle.wordWrap = true;
        textStyle.alignment = TextAnchor.UpperLeft;
        textStyle.normal.textColor = Color.black; 
        textStyle.fontStyle = FontStyle.Italic;

        // Text Rect with padding
        Rect textRect = new Rect(startX + 30, startY + 30, boxWidth - 60, boxHeight - 80);
        GUI.Label(textRect, noteText, textStyle);
        
        // Draw Close button
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 18;
        if (GUI.Button(new Rect(startX + (boxWidth / 2) - 60, startY + boxHeight - 45, 120, 30), "Close (Esc)", btnStyle))
        {
            PlayerStats stats = FindAnyObjectByType<PlayerStats>();
            if (stats != null) CloseNote(stats.gameObject);
        }
    }
}
