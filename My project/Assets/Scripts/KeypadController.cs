using UnityEngine;
using UnityEngine.Events;

public class KeypadController : MonoBehaviour, IInteractable
{
    [Header("Keypad Settings")]
    [SerializeField] private string passcode = "2024";
    [SerializeField] private ElectricTrap trapToDisable;
    public UnityEvent onUnlock;
    
    [Header("UI Status")]
    private bool isKeypadOpen = false;
    private string currentInput = "";
    private bool isSuccess = false;
    private bool isError = false;
    private float messageTimer = 0f;
    
    public string GetInteractPrompt()
    {
        return "Press E to use Keypad";
    }

    public void Interact(PlayerStats playerStats)
    {
        isKeypadOpen = !isKeypadOpen;
        
        if (isKeypadOpen)
        {
            // Lock player movement and camera
            PlayerController pc = playerStats.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = false;
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            currentInput = "";
            isSuccess = false;
            isError = false;
        }
        else
        {
            CloseKeypad(playerStats.gameObject);
        }
    }
    
    private void CloseKeypad(GameObject player)
    {
        isKeypadOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = true;
    }

    private void Update()
    {
        if (!isKeypadOpen) return;
        
        // Allow exiting with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            PlayerStats stats = FindAnyObjectByType<PlayerStats>();
            if (stats != null) CloseKeypad(stats.gameObject);
        }
        
        if (messageTimer > 0)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0)
            {
                isSuccess = false;
                isError = false;
                currentInput = "";
            }
        }
    }

    private void OnGUI()
    {
        if (!isKeypadOpen) return;

        // Draw a simple 2D Keypad in the center of the screen
        int boxWidth = 300;
        int boxHeight = 450;
        float startX = (Screen.width - boxWidth) / 2;
        float startY = (Screen.height - boxHeight) / 2;

        GUI.Box(new Rect(startX, startY, boxWidth, boxHeight), "SECURITY KEYPAD");

        // Display area
        GUIStyle displayStyle = new GUIStyle(GUI.skin.box);
        displayStyle.fontSize = 32;
        displayStyle.alignment = TextAnchor.MiddleCenter;
        
        if (isSuccess)
        {
            displayStyle.normal.textColor = Color.green;
            GUI.Box(new Rect(startX + 25, startY + 40, 250, 60), "ACCEPTED", displayStyle);
        }
        else if (isError)
        {
            displayStyle.normal.textColor = Color.red;
            GUI.Box(new Rect(startX + 25, startY + 40, 250, 60), "ERROR", displayStyle);
        }
        else
        {
            displayStyle.normal.textColor = Color.white;
            string displayStr = currentInput;
            while(displayStr.Length < 4) displayStr += "_";
            GUI.Box(new Rect(startX + 25, startY + 40, 250, 60), displayStr, displayStyle);
        }

        // Draw Buttons
        int btnWidth = 70;
        int btnHeight = 70;
        int spacing = 20;

        for (int i = 1; i <= 9; i++)
        {
            int row = (i - 1) / 3;
            int col = (i - 1) % 3;
            
            float btnX = startX + 25 + (col * (btnWidth + spacing));
            float btnY = startY + 120 + (row * (btnHeight + spacing));

            if (GUI.Button(new Rect(btnX, btnY, btnWidth, btnHeight), i.ToString()))
            {
                AddDigit(i.ToString());
            }
        }

        // Zero, Clear, Enter buttons
        float bottomRowY = startY + 120 + (3 * (btnHeight + spacing));
        
        if (GUI.Button(new Rect(startX + 25, bottomRowY, btnWidth, btnHeight), "C"))
        {
            currentInput = "";
        }
        
        if (GUI.Button(new Rect(startX + 25 + btnWidth + spacing, bottomRowY, btnWidth, btnHeight), "0"))
        {
            AddDigit("0");
        }
        
        if (GUI.Button(new Rect(startX + 25 + (2 * (btnWidth + spacing)), bottomRowY, btnWidth, btnHeight), "ENT"))
        {
            CheckPasscode();
        }
        
        // Exit button
        if (GUI.Button(new Rect(startX + 25, startY + boxHeight - 40, 250, 30), "EXIT"))
        {
            PlayerStats stats = FindAnyObjectByType<PlayerStats>();
            if (stats != null) CloseKeypad(stats.gameObject);
        }
    }

    private void AddDigit(string digit)
    {
        if (isSuccess || isError) return;
        
        if (currentInput.Length < 4)
        {
            currentInput += digit;
        }
    }

    private void CheckPasscode()
    {
        if (isSuccess || isError) return;

        if (currentInput == passcode)
        {
            isSuccess = true;
            messageTimer = 2f;
            
            if (trapToDisable != null)
            {
                trapToDisable.DisableTrap();
            }
            
            if (onUnlock != null)
            {
                onUnlock.Invoke();
            }
            
            // Close keypad after success
            Invoke("AutoClose", 2f);
        }
        else
        {
            isError = true;
            messageTimer = 1f;
        }
    }
    
    private void AutoClose()
    {
        PlayerStats stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null) CloseKeypad(stats.gameObject);
    }
}
