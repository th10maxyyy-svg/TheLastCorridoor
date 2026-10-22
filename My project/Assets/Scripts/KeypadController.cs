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

    /// <summary>True once the right code was entered (also after loading - it is saved).</summary>
    public bool IsUnlocked => isUnlocked;
    private int CodeLength => Mathf.Max(1, string.IsNullOrEmpty(passcode) ? 4 : passcode.Length);
    private float autoCloseTime = -1f;

    /// <summary>True while any keypad UI is on screen (other scripts use this to ignore G / mouse clicks).</summary>
    public static bool IsAnyKeypadOpen => openKeypadCount > 0;
    private static int openKeypadCount = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { openKeypadCount = 0; }

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
        openKeypadCount++;
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
        ShowUI(true);
    }

    private void CloseKeypad()
    {
        if (!isKeypadOpen) return;
        isKeypadOpen = false;
        openKeypadCount = Mathf.Max(0, openKeypadCount - 1);
        ShowUI(false);
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

        // Keyboard input as well as mouse clicks
        for (int d = 0; d <= 9; d++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d)) AddDigit(d.ToString());
        }
        if (Input.GetKeyDown(KeyCode.Backspace) && currentInput.Length > 0 && !isSuccess && !isError) currentInput = currentInput.Substring(0, currentInput.Length - 1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) CheckPasscode();

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






    // ------------------------------------------------------------------ on-screen keypad (TLC_UI canvas: Keypad)
    private void ShowUI(bool on)
    {
        TLCCanvas ui = on ? TLCCanvas.Ensure() : TLCCanvas.Instance;
        if (ui == null || ui.keypadPanel == null) return;
        if (on)
        {
            string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "ENT" };
            for (int i = 0; i < ui.keypadButtons.Length && i < labels.Length; i++)
            {
                string l = labels[i];
                ui.keypadButtons[i].onClick.RemoveAllListeners();
                ui.keypadButtons[i].onClick.AddListener(() => Press(l));
            }
            ui.keypadExit.onClick.RemoveAllListeners();
            ui.keypadExit.onClick.AddListener(CloseKeypad);
            RefreshUI();
        }
        ui.keypadPanel.SetActive(on);
    }

    /// <summary>A key on the on-screen keypad was clicked.</summary>
    public void Press(string label)
    {
        if (!isKeypadOpen) return;
        if (label == "C") { if (!isSuccess && !isError) currentInput = ""; }
        else if (label == "ENT") CheckPasscode();
        else AddDigit(label);
    }

    private void RefreshUI()
    {
        TLCCanvas ui = TLCCanvas.Instance;
        if (ui == null || ui.keypadDisplay == null) return;
        ui.keypadDisplay.fontSize = CodeLength > 4 ? 30 : 46;   // an 8-digit code must fit the display
        if (isSuccess) { ui.keypadDisplay.text = "ACCEPTED"; ui.keypadDisplay.color = Color.green; }
        else if (isError) { ui.keypadDisplay.text = "ERROR"; ui.keypadDisplay.color = Color.red; }
        else
        {
            string displayStr = "";
            int len = CodeLength;
            for (int d = 0; d < len; d++) displayStr += (d < currentInput.Length ? currentInput[d].ToString() : "_") + (d < len - 1 ? " " : "");
            ui.keypadDisplay.text = displayStr;
            ui.keypadDisplay.color = Color.white;
        }
    }

    private void LateUpdate()
    {
        if (isKeypadOpen) RefreshUI();
    }

    private void AddDigit(string digit)
    {
        if (isSuccess || isError) return;

        if (currentInput.Length < CodeLength)
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
