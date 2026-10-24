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

    /// <summary>Fired when the PLAYER opens a door (used by the one-time scares).</summary>
    public static System.Action<DoorController> PlayerOpened;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isInvalidDoorComponent = false;
    private string cachedDoorId;

    void Awake()
    {
        // 1. Empty door-frame container (like DoorD_V1 (1) in the Morgue) that holds child DoorControllers on its leaves:
        //    must NEVER rotate or unlock itself, otherwise the whole doorway moves out of place on respawn/reload.
        if (GetComponent<Renderer>() == null && GetComponent<Collider>() == null)
        {
            foreach (DoorController d in GetComponentsInChildren<DoorController>(true))
            {
                if (d != this)
                {
                    isInvalidDoorComponent = true;
                    isLocked = false;
                    enabled = false;
                    Destroy(this);
                    return;
                }
            }
        }

        // 2. Child window pane inside a door leaf (like DoorD_V2_Window inside DoorD_V2_Left):
        //    the parent leaf is the real door; the window pane must move with its parent leaf, not independently.
        if (transform.parent != null && transform.parent.GetComponent<DoorController>() != null && transform.parent.GetComponent<Renderer>() != null)
        {
            isInvalidDoorComponent = true;
            isLocked = false;
            enabled = false;
            Destroy(this);
            return;
        }

        cachedDoorId = SaveManager.GetSceneObjectId(transform);
    }

    void Start()
    {
        if (isInvalidDoorComponent) return;

        closedRotation = transform.localRotation;
        closedLocalPos = transform.localPosition;
        SetupSafeHinge();

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
            ApplyRotation(openRotation); // Snap to open immediately
        }

        // double doors: if the other leaf (or parent container in an older save) was already opened with the shared key, this leaf is unlocked too
        else if (isLocked && !string.IsNullOrEmpty(requiredKey) && transform.parent != null && SaveManager.instance != null)
        {
            if (SaveManager.instance.IsDoorUnlocked(SaveManager.GetSceneObjectId(transform.parent)))
            {
                isLocked = false;
                isOpen = true;
                ApplyRotation(openRotation);
            }
            else
            {
                foreach (DoorController d in transform.parent.GetComponentsInChildren<DoorController>(true))
                {
                    if (d == this || d.isInvalidDoorComponent) continue;
                    if (!string.Equals((d.requiredKey ?? "").Trim(), requiredKey.Trim(), System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (SaveManager.instance.IsDoorUnlocked(d.GetDoorID())) { isLocked = false; break; }
                }
            }
        }

        SetupEnemyBlocker();
    }

    void Update()
    {
        if (isInvalidDoorComponent) return;
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        ApplyRotation(Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * (Time.time < smoothBoostUntil ? smoothSpeed * 4f : smoothSpeed)));
        if (safeContents != null) UpdateSafeContents();
    }

    // ------------------------------------------------------------------ things inside a CLOSED safe can not be reached
    // (the note / the taser in the safe could be read or picked up straight through the closed door)
    private System.Collections.Generic.List<Collider> safeContents;
    private Bounds safeBounds;
    private bool contentsOpen = true;

    private static readonly System.Collections.Generic.List<DoorController> safes = new System.Collections.Generic.List<DoorController>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSafes() { safes.Clear(); }
    void OnDestroy() { safes.Remove(this); }

    /// <summary>True when this thing lies inside a safe that is still closed (it can not be reached through the door).</summary>
    public static bool BlockedBySafe(Component c)
    {
        if (c == null) return false;
        foreach (DoorController s in safes)
        {
            if (s == null || (s.isOpen && !s.isLocked)) continue;
            if (c.transform.IsChildOf(s.transform)) continue;          // the door / its keypad
            Bounds b = s.safeBounds; b.Expand(0.12f);
            if (b.Contains(c.transform.position)) return true;
            Collider col = c.GetComponent<Collider>();
            if (col != null && b.Contains(col.bounds.center)) return true;
            Renderer r = c.GetComponentInChildren<Renderer>();
            if (r != null && b.Contains(r.bounds.center)) return true;
        }
        return false;
    }

    private void CollectSafeContents()
    {
        Transform safe = transform.parent;
        bool any = false;
        foreach (Renderer r in safe.GetComponentsInChildren<Renderer>())
        {
            if (!any) { safeBounds = r.bounds; any = true; } else safeBounds.Encapsulate(r.bounds);
        }
        if (!any) return;
        if (!safes.Contains(this)) safes.Add(this);
        safeContents = new System.Collections.Generic.List<Collider>();
        foreach (Collider c in Physics.OverlapBox(safeBounds.center, safeBounds.extents * 0.98f, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
        {
            if (c == null || c.transform.IsChildOf(transform)) continue;   // the door itself and its keypad
            if (c.GetComponentInParent<IInteractable>() == null) continue;
            if (!safeBounds.Contains(c.bounds.center)) continue;
            safeContents.Add(c);
        }
        contentsOpen = true;
        UpdateSafeContents();
    }

    private void UpdateSafeContents()
    {
        bool open = isOpen && !isLocked;
        if (open == contentsOpen) return;
        contentsOpen = open;
        for (int i = safeContents.Count - 1; i >= 0; i--)
        {
            Collider c = safeContents[i];
            if (c == null || !safeBounds.Contains(c.transform.position)) { safeContents.RemoveAt(i); continue; }   // taken out
            c.enabled = open;
        }
    }

    // ------------------------------------------------------------------ safe doors swing on a side hinge
    // The safe models' door pivots sit in the middle of the safe and their axis was horizontal, so the door flipped UP
    // like a lid. A safe door now swings sideways around its own vertical edge, outwards.
    private Vector3 closedLocalPos;
    private bool useHinge;
    private Vector3 hingeLocal;   // hinge point in the door's own space

    private void ApplyRotation(Quaternion localRot)
    {
        transform.localRotation = localRot;
        if (!useHinge) return;
        Vector3 hingeParent = closedLocalPos + closedRotation * Vector3.Scale(hingeLocal, transform.localScale);
        transform.localPosition = hingeParent + (localRot * Quaternion.Inverse(closedRotation)) * (closedLocalPos - hingeParent);
    }

    private void SetupSafeHinge()
    {
        if (transform.parent == null || !transform.parent.name.ToLowerInvariant().StartsWith("safe")) return;
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;
        Vector3 c = box.center, sz = box.size;
        int thin = sz.x <= sz.y && sz.x <= sz.z ? 0 : sz.y <= sz.z ? 1 : 2;
        // of the two other axes: the one pointing up in the world is the hinge axis, the other one runs across the door
        int a1 = (thin + 1) % 3, a2 = (thin + 2) % 3;
        Vector3 v1 = Vector3.zero; v1[a1] = 1f; Vector3 v2 = Vector3.zero; v2[a2] = 1f;
        bool firstUp = Mathf.Abs(Vector3.Dot(transform.TransformDirection(v1).normalized, Vector3.up)) >= Mathf.Abs(Vector3.Dot(transform.TransformDirection(v2).normalized, Vector3.up));
        int up = firstUp ? a1 : a2, across = firstUp ? a2 : a1;
        Vector3 axis = Vector3.zero; axis[up] = 1f;
        Vector3 acr = Vector3.zero; acr[across] = 1f;
        // the hinge is on the side away from the keypad
        float side = 1f;
        KeypadController pad = GetComponentInChildren<KeypadController>(true);
        if (pad != null && pad.transform != transform)
        {
            float k = transform.InverseTransformPoint(pad.transform.position)[across] - c[across];
            side = k > 0f ? -1f : 1f;
        }
        hingeLocal = c + acr * (sz[across] * 0.5f * side);
        rotationAxis = axis;
        useHinge = true;
        CollectSafeContents();
        // open OUTWARDS: the free edge must move to the front (the side of the door away from the pivot / safe body)
        Vector3 front = Vector3.zero; front[thin] = Mathf.Sign(c[thin]) == 0f ? 1f : Mathf.Sign(c[thin]);
        Vector3 freeEdge = c - acr * (sz[across] * 0.5f * side) - hingeLocal;
        float ang = Mathf.Max(90f, Mathf.Abs(openAngle));
        Vector3 plus = Quaternion.AngleAxis(ang, axis) * freeEdge;
        openAngle = Vector3.Dot(plus, front) >= 0f ? ang : -ang;
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
                UnlockPartners();

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
            PlayerOpened?.Invoke(this);
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

    /// <summary>Scare: the open door bangs shut on its own (it is not locked - the player can open it again).</summary>
    public void SlamShut()
    {
        if (!isOpen || isLocked) return;
        isOpen = false;
        if (audioSource != null && closeSound != null) { audioSource.pitch = 0.8f; audioSource.PlayOneShot(closeSound, 1f); audioSource.pitch = 1f; }
        smoothBoostUntil = Time.time + 0.6f;
    }
    private float smoothBoostUntil;

    // --- TESTING ONLY (The Last Corridor > Testing > Unlock All Doors): nothing here is ever saved ---
    private bool testUnlocked;
    /// <summary>Unlocked only by the testing switch (counts as LOCKED for objectives, saves and achievements).</summary>
    public bool IsTestUnlocked => testUnlocked;
    private bool IsSafeOrCodeLock()
    {
        string k = (requiredKey ?? "").ToLowerInvariant();
        if (k.Contains("code")) return true;
        for (Transform t = transform; t != null; t = t.parent) if (t.name.ToLowerInvariant().StartsWith("safe")) return true;
        return false;
    }
    /// <summary>on = unlock for testing (not saved); off = lock again every door that the test mode had unlocked.</summary>
    public void SetTestUnlocked(bool on)
    {
        if (isInvalidDoorComponent) return;
        // safes and code locks keep their puzzle: the testing switch only opens room doors
        if (on && (IsSafeOrCodeLock())) return;
        if (on)
        {
            if (isLocked) { testUnlocked = true; isLocked = false; }
            return;
        }
        if (!testUnlocked) return;
        testUnlocked = false;
        if (SaveManager.instance != null && SaveManager.instance.IsDoorUnlocked(GetDoorID())) return;   // really unlocked meanwhile
        if (isOpen) SlamShut();
        isLocked = true;
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
        if (string.IsNullOrEmpty(cachedDoorId)) cachedDoorId = SaveManager.GetSceneObjectId(transform);
        return cachedDoorId;
    }

    /// <summary>
    /// BUG FIX: double doors (Morgue, Surgery, Visiting Room, Dining Room) have one DoorController per leaf, all needing
    /// the same key. The key is used up by the first leaf, so the other leaf stayed locked for ever. Now every leaf of
    /// the same door (same parent / the door root) that needs the same key is unlocked too.
    /// </summary>
    private void UnlockPartners()
    {
        Transform top = transform.parent;
        if (top == null) return;
        foreach (DoorController d in top.GetComponentsInChildren<DoorController>(true))
        {
            if (d == this || d.isInvalidDoorComponent || !d.isLocked) continue;
            if (!string.Equals((d.requiredKey ?? "").Trim(), (requiredKey ?? "").Trim(), System.StringComparison.OrdinalIgnoreCase)) continue;
            d.isLocked = false;
            d.SaveDoorState();
        }
    }

    private void SaveDoorState()
    {
        if (SaveManager.instance != null)
        {
            SaveManager.instance.UnlockDoor(GetDoorID());
        }
    }
}
