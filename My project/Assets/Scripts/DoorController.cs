using UnityEngine;
using UnityEngine.Events;
public class DoorController : MonoBehaviour, IInteractable
{
    [Header("Lock Settings")]
    [SerializeField] private bool isLocked = false;
    [SerializeField] private string requiredKey = "RedKey";

    [Header("Movement Settings")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up; // (0,1,0) by default
    [SerializeField] private float openAngle = 90.0f;
    [SerializeField] private float smoothSpeed = 5.0f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    [Header("Room Name")]
    [Tooltip("Optional. Name shown on screen when the player looks at this door. Leave empty = taken automatically from the room it belongs to in the Hierarchy (e.g. GuardRoom -> \"Guard Room\").")]
    [SerializeField] private string roomNameOverride = "";

    [Header("Events")]
    public UnityEvent onDoorOpen;
    [Tooltip("Fires when player tries to open but it's locked")]
    public UnityEvent onLockedInteract;
    
    [Header("Horror Events")]
    [Tooltip("If this door should trigger an ambush when unlocked, assign it here.")]
    public CinematicAmbush ambushToTrigger;

    private bool isOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    void Start()
    {
        closedRotation = transform.localRotation;
        
        // Calculate target rotation based on the chosen axis
        openRotation = closedRotation * Quaternion.AngleAxis(openAngle, rotationAxis);

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f; // Make it 3D sound
            }
        }

        // WORLD STATE CHECK: Agar ye darwaza pehle se unlock tha to isay automatically open kar do!
        if (SaveManager.instance != null && SaveManager.instance.IsDoorUnlocked(GetDoorID()))
        {
            isLocked = false;
            isOpen = true;
            transform.localRotation = openRotation; // Snap to open immediately
        }

        SetupEnemyBlocker();
    }

    void Update()
    {
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * smoothSpeed);
    }
    
    // Can be called by KeypadController's OnUnlock Event
    public void UnlockDoor()
    {
        MarkDiscovered();
        isLocked = false;
        isOpen = true; // Automatically open when unlocked via keypad
        if (audioSource != null && openSound != null)
        {
            audioSource.PlayOneShot(openSound);
        }
        SaveDoorState();
    }

    public string GetInteractPrompt()
    {
        if (isTrapLocked) return "The door won't budge!";

        if (isLocked)
        {
            // Puzzle wale darwazon ke liye "Needs ." na likha aaye
            if (string.IsNullOrEmpty(requiredKey)) return "Door is Locked.";
            return "Door is Locked. Needs " + requiredKey + ".";
        }
        
        return isOpen ? "Press E to Close Door" : "Press E to Open Door";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isTrapLocked)
        {
            if (onLockedInteract != null) onLockedInteract.Invoke();
            return;
        }

        if (isLocked)
        {
            // NAYA FIX: Agar Required Key empty hai, to matlab isay inventory se nahi, kisi Puzzle se khulna hai!
            if (string.IsNullOrEmpty(requiredKey))
            {
                Debug.Log("Door is locked via puzzle!");
                if (onLockedInteract != null) onLockedInteract.Invoke();
                return;
            }

            Inventory inventory = playerStats.GetComponent<Inventory>();
            if (inventory != null && inventory.HasItem(requiredKey))
            {
                isLocked = false;
                ToggleDoor();
                Debug.Log("Door unlocked and opened!");
                
                // NAYA FIX: Key use hone ke baad usay inventory se khatam (consume) kar do!
                inventory.RemoveItem(requiredKey, false); // false = darwaza hamesha ke liye khul gaya, key wapas nahi aayegi
                
                SaveDoorState();

                // Auto-Trigger Cinematic Ambush! 
                if (ambushToTrigger != null)
                {
                    ambushToTrigger.TriggerAmbush(true); // TRUE = Force Chase!
                }
                else if (!string.IsNullOrEmpty(requiredKey) && requiredKey.ToLower().Contains("guard"))
                {
                    // FALLBACK: Agar inspector mein assign karna bhool gaye to qareeb tareen ambush khud dhoondo!
                    CinematicAmbush[] ambushes = FindObjectsByType<CinematicAmbush>();
                    CinematicAmbush closestAmbush = null;
                    float minDistance = Mathf.Infinity;
                    
                    foreach (var ambush in ambushes)
                    {
                        float dist = Vector3.Distance(transform.position, ambush.transform.position);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            closestAmbush = ambush;
                        }
                    }

                    if (closestAmbush != null && minDistance < 10f) // Door ke paas hona chahiye
                    {
                        closestAmbush.TriggerAmbush(true); // TRUE = Force Chase!
                        Debug.LogWarning("DoorController: ambushToTrigger inspector mein khali tha, lekin auto-fallback ne qareeb tareen ambush trigger kar diya!");
                    }
                    else
                    {
                        Debug.LogWarning("DoorController: Koi CinematicAmbush aas paas nahi mila!");
                    }
                }
            }
            else
            {
                Debug.Log("Door is locked!");
                if (onLockedInteract != null)
                {
                    onLockedInteract.Invoke();
                }
            }
        }
        else
        {
            ToggleDoor();
        }
    }

    private void ToggleDoor()
    {
        isOpen = !isOpen;
        if (isOpen) MarkDiscovered();   // only the player calls ToggleDoor

        // Play sound and trigger events
        if (isOpen)
        {
            if (audioSource != null && openSound != null)
                audioSource.PlayOneShot(openSound);
                
            // Naya code: Jab darwaza khulega to ye event trigger hoga
            if (onDoorOpen != null)
                onDoorOpen.Invoke();
        }
        else
        {
            if (audioSource != null && closeSound != null)
                audioSource.PlayOneShot(closeSound);
        }
    }
    
    public bool IsOpen => isOpen;
    public bool IsLocked => isLocked;

    // ------------------------------------------------------------------ rooms the player has not discovered yet
    // The clown may NOT walk into a room the player has never opened: a NavMeshObstacle blocks the doorway for his
    // pathfinding until the player opens that door once (remembered in the save). Otherwise he could open an unknown
    // door, go in, and the player would simply follow him inside.
    public bool PlayerDiscovered { get; private set; }
    private UnityEngine.AI.NavMeshObstacle enemyBlocker;
    private string SeenFlag => "DoorSeen_" + GetDoorID();

    private void SetupEnemyBlocker()
    {
        PlayerDiscovered = isOpen || (SaveManager.instance != null && SaveManager.instance.HasFlag(SeenFlag));
        if (string.IsNullOrEmpty(RoomName)) return;                  // only real room doors
        Collider col = GetComponentInChildren<Collider>();
        if (col == null) return;
        Bounds b = col.bounds;
        GameObject g = new GameObject(name + "_EnemyBlocker");
        g.transform.position = b.center;
        enemyBlocker = g.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        enemyBlocker.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
        enemyBlocker.size = new Vector3(Mathf.Max(b.size.x, 0.2f), Mathf.Max(b.size.y, 1.5f), Mathf.Max(b.size.z, 0.2f));
        enemyBlocker.carving = true;
        enemyBlocker.carveOnlyStationary = true;
        enemyBlocker.enabled = !PlayerDiscovered;
    }

    /// <summary>The player opened this door himself: from now on the clown may use it too.</summary>
    public void MarkDiscovered()
    {
        if (PlayerDiscovered) return;
        PlayerDiscovered = true;
        if (enemyBlocker != null) enemyBlocker.enabled = false;
        if (SaveManager.instance != null) SaveManager.instance.SetFlag(SeenFlag);
    }

    /// <summary>The clown pushes an (unlocked) closed door open when he walks through it.</summary>
    public void OpenByEnemy()
    {
        if (isOpen || isLocked || isTrapLocked || !PlayerDiscovered) return;
        isOpen = true;
        if (audioSource != null && openSound != null) audioSource.PlayOneShot(openSound);
    }
    public string RequiredKey => requiredKey;
    public string DoorID => GetDoorID();

    // ------------------------------------------------------------------ room name shown when the player looks at a door
    private string cachedRoomName, cachedFloorName;

    /// <summary>Room this door leads into ("" for cupboards, fridges, toilet stalls etc. - only real room doors get a name).</summary>
    public string RoomName { get { if (cachedRoomName == null) ResolveRoomName(); return cachedRoomName; } }
    public string FloorName { get { if (cachedRoomName == null) ResolveRoomName(); return cachedFloorName; } }

    private static readonly System.Collections.Generic.Dictionary<string, string> PrettyRooms = new System.Collections.Generic.Dictionary<string, string>
    {
        { "GuardRoom", "Guard Room" }, { "DayRoom", "Day Room" }, { "Stock", "Stock Room" }, { "DiningRoom", "Dining Room" },
        { "Kitchen", "Kitchen" }, { "KitchenA", "Kitchen" }, { "Office", "Head Office" }, { "OfficeA", "Office" },
        { "ToiletsMan", "Men's Toilets" }, { "ToiletsManA", "Men's Toilets" }, { "ToiletsWoman", "Women's Toilets" }, { "ToiletsWomanA", "Women's Toilets" },
        { "Bathroom", "Bathroom" }, { "Surgery", "Surgery" }, { "ExamRoom", "Exam Room" }, { "Classroom", "Classroom" },
        { "Bedroom", "Visiting Room" }, { "Gatehouse", "Gatehouse" }, { "HallDownstair", "Main Hall" }, { "HallUpstair", "Upper Hall" },
        { "HallUnderground", "Basement Hall" }, { "Morgue", "Morgue" }, { "MachineRoom", "Machine Room" },
        { "SeclusionRoomA", "Seclusion Room A" }, { "SeclusionRoomB", "Seclusion Room B" },
    };

    private void ResolveRoomName()
    {
        cachedRoomName = ""; cachedFloorName = "";
        if (!string.IsNullOrEmpty(roomNameOverride)) { cachedRoomName = roomNameOverride; return; }

        // only real doors: "Door", "Door_V1 (2)", "DoorD_V2_Left", "MainDoor" - not cupboards, fridges, cases, mirrors, toilet stalls, safes
        if (!IsRoomDoorName(name) && (transform.parent == null || !IsRoomDoorName(transform.parent.name))) return;

        for (Transform t = transform; t != null && t.parent != null; t = t.parent)
        {
            string floor = t.parent.name;
            if (floor == "1stFloor" || floor == "2ndFloor" || floor == "Basement")
            {
                string key = t.name;
                string pretty;
                if (!PrettyRooms.TryGetValue(key, out pretty)) pretty = SplitWords(key);
                cachedRoomName = pretty;
                cachedFloorName = floor == "1stFloor" ? "Ground Floor" : floor == "2ndFloor" ? "First Floor" : "Basement";
                return;
            }
        }
    }

    private static bool IsRoomDoorName(string n)
    {
        if (string.IsNullOrEmpty(n)) return false;
        return n.StartsWith("Door") || n.StartsWith("MainDoor");
    }

    private static string SplitWords(string s)
    {
        System.Text.StringBuilder b = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1])) b.Append(' ');
            b.Append(c);
        }
        return b.ToString().Trim();
    }

    /// <summary>Used by the end cinematic: unlocks and silently swings the door open.</summary>
    public void OpenForCinematic()
    {
        MarkDiscovered();
        isLocked = false;
        isTrapLocked = false;
        if (isOpen) return;
        isOpen = true;
        SaveDoorState();
    }

    // External method to unlock the door from puzzles (like a Generator)
    public void ForceUnlock()
    {
        isLocked = false;
        Debug.Log("Door " + gameObject.name + " unlocked externally!");
        SaveDoorState();
    }

    // --- TRAP LOCK (used by ToxicGasTrap instead of hacking private fields with reflection) ---
    private bool isTrapLocked = false;

    /// <summary>Slams the door shut and keeps it shut until ReleaseTrapLock() is called.</summary>
    public void TrapLock()
    {
        if (isTrapLocked) return;
        isTrapLocked = true;

        if (isOpen)
        {
            isOpen = false;
            if (audioSource != null && closeSound != null) audioSource.PlayOneShot(closeSound);
        }
    }

    public void ReleaseTrapLock()
    {
        isTrapLocked = false;
    }

    // Helper to make a unique ID for this specific door, in case many doors are named "Door"
    private string GetDoorID()
    {
        return SaveManager.GetSceneObjectId(transform);
    }

    private void SaveDoorState()
    {
        if (SaveManager.instance != null)
        {
            SaveManager.instance.UnlockDoor(GetDoorID());
        }
    }
}
