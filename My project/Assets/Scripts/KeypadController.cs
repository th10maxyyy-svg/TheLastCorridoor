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
    private bool isUnlocked = false;
    private float autoCloseTime = -1f;

    private PlayerStats activePlayer;
    private float healthWhenOpened;
    private string saveFlag;

    void Start()
    {
        // WORLD STATE: code pehle sahi daala ja chuka hai to keypad dobara na maange
        saveFlag = "Keypad_" + SaveManager.GetSceneObjectId(transform);
        if (SaveManager.instance != null && SaveManager.instance.HasFlag(saveFlag))
        {
            isUnlocked = true;
            if (trapToDisable != null) trapToDisable.DisableTrap();
            // Re-apply what the keypad unlocked (drawers, revealed objects...) since those aren't saved on their own
            onUnlock?.Invoke();
        }
    }

    public string GetInteractPrompt()
    {
        if (isUnlocked && !isKeypadOpen) return "Keypad: ACCESS GRANTED";
        return "Press E to use Keypad";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isKeypadOpen)
        {
            CloseKeypad();
            return;
        }

        if (isUnlocked) return;
        OpenKeypad(playerStats);
    }

    private void OpenKeypad(PlayerStats playerStats)
    {
        isKeypadOpen = true;
        activePlayer = playerStats;
        healthWhenOpened = playerStats != null ? playerStats.GetHealth() : 0f;
        autoCloseTime = -1f;

        // Lock player movement and camera
        PlayerController pc = playerStats != null ? playerStats.GetComponent<PlayerController>() : null;
        if (pc != null) pc.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        currentInput = "";
        isSuccess = false;
        isError = false;
    }

    private void CloseKeypad()
    {
        if (!isKeypadOpen) return;
        isKeypadOpen = false;
        autoCloseTime = -1f;

        bool playerDead = activePlayer == null || activePlayer.IsDead();
        if (!playerDead)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            PlayerController pc = activePlayer.GetComponent<PlayerController>();
            if (pc != null && !pc.isDead) pc.enabled = true;
        }
        activePlayer = null;
    }

    private void Update()
    {
        if (!isKeypadOpen) return;

        // Enemy ne maara ya player mar gaya -> keypad foran band, taake player bhaag sake
        if (activePlayer == null || activePlayer.IsDead() || activePlayer.GetHealth() < healthWhenOpened)
        {
            CloseKeypad();
            return;
        }

        // Allow exiting with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseKeypad();
            return;
        }

        if (autoCloseTime > 0f && Time.time >= autoCloseTime)
        {
            CloseKeypad();
            return;
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

    private void OnDisable()
    {
        CloseKeypad();
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
            CloseKeypad();
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
            isUnlocked = true;
            messageTimer = 2f;
            if (SaveManager.instance != null) SaveManager.instance.SetFlag(saveFlag);

            if (trapToDisable != null)
            {
                trapToDisable.DisableTrap();
            }

            if (onUnlock != null)
            {
                onUnlock.Invoke();
            }

            // Close keypad after success (only this opening of it - no stray Invoke any more)
            autoCloseTime = Time.time + 2f;
        }
        else
        {
            isError = true;
            messageTimer = 1f;
        }
    }
}
