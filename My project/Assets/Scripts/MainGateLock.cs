using UnityEngine;

/// <summary>
/// THE MAIN GATE needs two things before the player can escape:
///   1. the MAIN GATE KEY - found in the last room (it rises out of the drained acid bath in the Bathroom)
///   2. the 8-DIGIT GATE CODE - typed into the gate keypad. Its 8 digits are written on 8 notes hidden around the asylum
///      ("GATE CODE - digit 3 of 8: 9"). The notes are kept in the Journal (TAB > NOTES).
/// Sits on the "Main Gate Lock" object (a trigger box around the exit door, so looking at the door shows this prompt).
/// Both parts are saved (key: flag "GateKey_Main Gate Key", code: the keypad's own flag), so they survive deaths / loading.
/// When both are done the gate unlocks and the Grand Escape cinematic starts.
/// Until then the escape trigger in front of the gate does nothing (EscapeCinematicManager asks AllowEscape).
/// Created by the menu: The Last Corridor / Setup Main Gate (key + code).
/// </summary>
public class MainGateLock : MonoBehaviour, IInteractable
{
    [Tooltip("keyName of the key the gate needs (same spelling as on the KeyItem)")]
    public string requiredKey = "Main Gate Key";
    [Tooltip("The gate's keypad (child object 'Gate Keypad'); its passcode is the 8-digit code")]
    public KeypadController keypad;

    public EscapeCinematicManager escape;
    public DoorController gateDoor;
    public AudioClip insertSound;

    private static MainGateLock instance;
    private AudioSource src;
    private bool escaping;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    /// <summary>True when there is no gate lock in the level, or both the key and the code are done.</summary>
    public static bool AllowEscape => instance == null || instance.IsUnlocked;

    /// <summary>Extra text for the escape objective.</summary>
    public static string ProgressText()
    {
        if (instance == null) return "";
        return "(key: " + (instance.KeyInserted ? "in the gate" : "missing") + ", code: " + (instance.CodeEntered ? "entered" : "8 digits needed") + ")";
    }

    public bool KeyInserted => SaveManager.instance != null && SaveManager.instance.HasFlag("GateKey_" + requiredKey);
    public bool CodeEntered => keypad == null || keypad.IsUnlocked;
    public bool IsUnlocked => KeyInserted && CodeEntered;

    void Awake()
    {
        instance = this;
        src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 1f; src.minDistance = 2f; src.playOnAwake = false;
        if (escape == null) escape = FindAnyObjectByType<EscapeCinematicManager>(FindObjectsInactive.Include);
        if (keypad == null) keypad = GetComponentInChildren<KeypadController>(true);
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void Update()
    {
        // the code was just accepted on the keypad (or the key went in) -> open the gate
        if (!escaping && IsUnlocked && !KeypadController.IsAnyKeypadOpen && keypadWasUsed)
        {
            keypadWasUsed = false;
            StartEscape(FindAnyObjectByType<PlayerStats>());
        }
    }
    private bool keypadWasUsed;

    private int codeNotesTotal = -1;
    private readonly System.Collections.Generic.List<string> codeNoteIds = new System.Collections.Generic.List<string>();

    /// <summary>How many of the 8 gate-code notes the player has read (and how many exist).</summary>
    public int CodeNotesRead(out int total)
    {
        if (codeNotesTotal < 0)
        {
            foreach (ReadableNote n in FindObjectsByType<ReadableNote>(FindObjectsInactive.Include))
                if (n.name.StartsWith("GateCodeNote_")) codeNoteIds.Add(SaveManager.GetSceneObjectId(n.transform));
            codeNotesTotal = codeNoteIds.Count;
        }
        total = codeNotesTotal;
        SaveData d = GameProgress.CurrentSave;
        if (d == null) return 0;
        var read = SaveManager.SplitList(d.notesReadCSV);
        int c = 0;
        foreach (string id in codeNoteIds) if (read.Contains(id)) c++;
        return c;
    }

    private string CodeHint()
    {
        int read = CodeNotesRead(out int total);
        return total > 0 ? "Code notes found: " + read + "/" + total + " (TAB > Notes)" : "";
    }

    private bool HoldingKey(PlayerStats ps)
    {
        Inventory inv = ps != null ? ps.GetComponent<Inventory>() : FindAnyObjectByType<Inventory>();
        return inv != null && inv.HasItem(requiredKey);
    }

    public string GetInteractPrompt()
    {
        if (escaping) return "";
        if (IsUnlocked) return "";
        if (!KeyInserted && HoldingKey(FindAnyObjectByType<PlayerStats>())) return "Press E to put the " + requiredKey + " in the gate";
        if (!CodeEntered && KeyInserted) return "Press E to enter the gate code (8 digits)   " + CodeHint();
        if (!CodeEntered) return "MAIN GATE - locked. Press E for the code keypad.  Key: " + (KeyInserted ? "in" : "missing") + "   " + CodeHint();
        return "MAIN GATE - needs the " + requiredKey;
    }

    public void Interact(PlayerStats playerStats)
    {
        if (escaping) return;
        if (IsUnlocked) { StartEscape(playerStats); return; }

        // 1. the key, if the player is holding it
        if (!KeyInserted && HoldingKey(playerStats))
        {
            Inventory inv = playerStats.GetComponent<Inventory>();
            if (inv != null) inv.RemoveItem(requiredKey, false);        // stays in the gate for good
            if (SaveManager.instance != null) { SaveManager.instance.SetFlag("GateKey_" + requiredKey); SaveManager.instance.SaveWorldState(); }
            if (insertSound != null) src.PlayOneShot(insertSound);
            if (IsUnlocked)
            {
                StartEscape(playerStats);
            }
            else InteractionRaycast.Notify(playerStats, "The key turns, but the gate is still locked. It needs the 8-digit code.", 3.5f);
            return;
        }

        // 2. the code keypad
        if (!CodeEntered && keypad != null)
        {
            keypadWasUsed = true;
            keypad.Interact(playerStats);
            return;
        }

        InteractionRaycast.Notify(playerStats, "You need the " + requiredKey + ".", 3f);
    }

    private void StartEscape(PlayerStats ps)
    {
        if (escaping) return;
        escaping = true;
        if (escape != null && ps != null) escape.BeginEscape(ps.gameObject, 0.15f);   // straight into the cinematic
    }
}
