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
    }

    private void CloseKeypad()
    {
        if (!isKeypadOpen) return;
        isKeypadOpen = false;
        openKeypadCount = Mathf.Max(0, openKeypadCount - 1);
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

    private Texture2D panelTex, displayTex;
    private GUIStyle titleStyle, displayStyle, buttonStyle, smallButtonStyle;

    private static Texture2D MakeTex(Color c)
    {
        Texture2D t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }

    private void BuildStyles()
    {
        panelTex = MakeTex(new Color(0.07f, 0.07f, 0.08f, 0.97f));
        displayTex = MakeTex(new Color(0.02f, 0.03f, 0.03f, 1f));

        titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);

        displayStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 34, fontStyle = FontStyle.Bold };
        displayStyle.normal.background = displayTex;

        buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 26, fontStyle = FontStyle.Bold };
        smallButtonStyle = new GUIStyle(buttonStyle) { fontSize = 18 };
    }

    private void OnGUI()
    {
        if (!isKeypadOpen) return;
        if (panelTex == null) BuildStyles();

        // Layout is designed on a 300 x 500 panel and scaled with the screen so it never overflows or overlaps
        const float W = 300f, H = 500f;
        float s = Mathf.Clamp(Screen.height / 800f, 0.8f, 2.5f);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - W * s) * 0.5f, (Screen.height - H * s) * 0.5f, 0f), Quaternion.identity, new Vector3(s, s, 1f));

        // Panel background (opaque, so the 3D keypad behind it does not shine through)
        GUI.DrawTexture(new Rect(0, 0, W, H), panelTex);
        GUI.Label(new Rect(0, 8, W, 24), "SECURITY KEYPAD", titleStyle);

        // Display
        if (isSuccess)
        {
            displayStyle.normal.textColor = Color.green;
            GUI.Box(new Rect(25, 40, 250, 60), "ACCEPTED", displayStyle);
        }
        else if (isError)
        {
            displayStyle.normal.textColor = Color.red;
            GUI.Box(new Rect(25, 40, 250, 60), "ERROR", displayStyle);
        }
        else
        {
            displayStyle.normal.textColor = Color.white;
            string displayStr = "";
            for (int d = 0; d < 4; d++) displayStr += (d < currentInput.Length ? currentInput[d].ToString() : "_") + (d < 3 ? " " : "");
            GUI.Box(new Rect(25, 40, 250, 60), displayStr, displayStyle);
        }

        // 4 x 3 grid: 1 2 3 / 4 5 6 / 7 8 9 / C 0 ENT
        const float bw = 76f, bh = 64f, gap = 11f, left = 25f, top = 116f;
        string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "ENT" };
        for (int i = 0; i < labels.Length; i++)
        {
            int row = i / 3, col = i % 3;
            Rect r = new Rect(left + col * (bw + gap), top + row * (bh + gap), bw, bh);
            bool special = labels[i] == "C" || labels[i] == "ENT";
            if (GUI.Button(r, labels[i], special ? smallButtonStyle : buttonStyle))
            {
                if (labels[i] == "C") { if (!isSuccess && !isError) currentInput = ""; }
                else if (labels[i] == "ENT") CheckPasscode();
                else AddDigit(labels[i]);
            }
        }

        // Exit button (below the grid: grid ends at 116 + 4*64 + 3*11 = 405)
        if (GUI.Button(new Rect(25, 428, 250, 48), "EXIT", smallButtonStyle))
        {
            CloseKeypad();
        }

        GUI.matrix = oldMatrix;
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
