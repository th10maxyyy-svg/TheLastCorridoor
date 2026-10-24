using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OBJECTIVES (FR_48, FR_49, FR_31) - a STORY CHAIN: the player always sees exactly ONE thing to do next.
///  1. Find the Guard Room key            (start of the game)
///  2. ESCAPE THE BASEMENT (6 steps)       (after the clown drags him down)
///  3. One objective per room upstairs     (surgery, exam room, kitchens, visiting room, office, bathroom...)
///  4. Escape the asylum through the main gate
/// A step is completed as soon as its condition is true (key picked up, door unlocked, puzzle solved...) - also when
/// the player does things out of order. Completed steps are stored as events in the save file, so they survive deaths,
/// respawns and loading. Steps whose objects are missing from the scene are left out automatically.
/// To change the order or the texts: edit BuildChain() below - every step is one line.
/// Created at runtime by GameUI inside the game level.
/// </summary>
public class ObjectiveTracker : MonoBehaviour
{
    public const string DonePrefix = "OBJ_";
    public const string FoundPrefix = "OBJD_";

    public static ObjectiveTracker Instance { get; private set; }
    public static int LastKnownTotal { get; private set; } = 0;

    public class Objective
    {
        public string id;
        public string title;
        public bool alwaysVisible;
        public int order;
        public List<Transform> anchors = new List<Transform>();
        public System.Func<bool> isDone;           // live check
        public System.Func<string> progressText;   // optional extra text, e.g. "- key found, open the Guard Room"
        public bool discovered;                    // reached in the chain (shown in the journal)
        public bool done;
    }

    private readonly List<Objective> objectives = new List<Objective>();
    public IReadOnlyList<Objective> Objectives => objectives;

    private int currentIndex = 0;
    private int lastAnnouncedIndex = -1;
    private float nextPoll;
    private Transform player;
    private Inventory inventory;
    private bool built;

    // silent achievement trackers (not in the list)
    private readonly List<string> keypadFlags = new List<string>();
    private bool hasHeadlamp;

    /// <summary>True while the very first objective (the Guard Room key) is the active one.</summary>
    public bool IsGated => built && currentIndex == 0 && objectives.Count > 0 && objectives[0].id == "GuardKey";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; LastKnownTotal = 0; }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        BuildChain();
        RefreshStates(false);
        Objective cur = Current();
        if (cur != null) GameUI.Toast("OBJECTIVE", TitleWithProgress(cur));   // tell the player right away what to do
        lastAnnouncedIndex = currentIndex;
    }

    // ============================================================================================ scene lookups
    private static string IdOf(Transform t) => SaveManager.GetSceneObjectId(t);
    private static bool Flag(string flag) => SaveManager.instance != null && SaveManager.instance.HasFlag(flag);

    private readonly Dictionary<string, List<GameObject>> keyObjects = new Dictionary<string, List<GameObject>>();
    private readonly Dictionary<string, List<string>> keyIds = new Dictionary<string, List<string>>();

    private bool KeyExists(string keyName) => keyObjects.ContainsKey(keyName.ToLower());

    /// <summary>True once a key/item with this name has ever been picked up (or is in the hand right now).</summary>
    private bool KeyTaken(string keyName)
    {
        string k = keyName.ToLower();
        SaveManager sm = SaveManager.instance;
        if (sm != null)
        {
            if (!keyIds.TryGetValue(k, out List<string> ids))
            {
                ids = new List<string>();
                if (keyObjects.TryGetValue(k, out List<GameObject> gos))
                    foreach (GameObject g in gos) if (g != null) ids.Add(sm.GetItemId(g));
                keyIds[k] = ids;
            }
            foreach (string id in ids) if (!string.IsNullOrEmpty(id) && sm.IsItemCollected(id)) return true;
        }
        if (inventory == null) inventory = FindAnyObjectByType<Inventory>();
        return inventory != null && inventory.HasItem(keyName);
    }

    /// <summary>The locked door of a room (by its name in the Hierarchy, e.g. "GuardRoom"), optionally the one that needs this key.</summary>
    private static DoorController LockedDoor(string room, string key = null)
    {
        foreach (DoorController d in FindObjectsByType<DoorController>(FindObjectsInactive.Include))
        {
            if (!d.IsLocked && !d.IsTestUnlocked) continue;   // a door opened by the testing switch is still 'locked' for the story
            if (key != null && (d.RequiredKey == null || d.RequiredKey.ToLower() != key.ToLower())) continue;
            for (Transform t = d.transform; t != null; t = t.parent)
                if (t.name == room) return d;
        }
        return null;
    }

    private static bool DoorOpened(DoorController d, string id)
    {
        if (d != null && !d.IsLocked && !d.IsTestUnlocked) return true;
        return SaveManager.instance != null && !string.IsNullOrEmpty(id) && SaveManager.instance.IsDoorUnlocked(id);
    }

    private static T InRoom<T>(string room) where T : Component
    {
        foreach (T c in FindObjectsByType<T>(FindObjectsInactive.Include))
            for (Transform t = c.transform; t != null; t = t.parent)
                if (t.name == room) return c;
        return null;
    }

    private Objective Add(string id, string title, System.Func<bool> isDone)
    {
        Objective o = new Objective { id = id, title = title, order = objectives.Count, isDone = isDone };
        objectives.Add(o);
        return o;
    }

    // ============================================================================================ THE CHAIN
    private void BuildChain()
    {
        objectives.Clear();
        keyObjects.Clear();
        keyIds.Clear();
        FindObjectsInactive inc = FindObjectsInactive.Include;

        foreach (KeyItem k in FindObjectsByType<KeyItem>(inc))
        {
            string n = (k.GetKeyName() ?? "").ToLower();
            if (n.Length == 0) continue;
            if (!keyObjects.TryGetValue(n, out List<GameObject> list)) keyObjects[n] = list = new List<GameObject>();
            list.Add(k.gameObject);
        }
        if (inventory == null) inventory = FindAnyObjectByType<Inventory>();

        // ---------------------------------------------------------------- 1. the start: the Guard Room
        DoorController guardDoor = null;
        foreach (DoorController d in FindObjectsByType<DoorController>(inc))
            if (!string.IsNullOrEmpty(d.RequiredKey) && d.RequiredKey.ToLower().Contains("guard")) { guardDoor = d; break; }
        if (guardDoor != null)
        {
            DoorController gd = guardDoor; string gid = gd.DoorID; string gkey = gd.RequiredKey;
            Objective o = Add("GuardKey", "Find the Guard Room key to unlock it", () => DoorOpened(gd, gid));
            o.progressText = () => KeyTaken(gkey) ? "- key found, open the Guard Room" : "";
        }

        // ---------------------------------------------------------------- 2. ESCAPE THE BASEMENT
        const string B = "ESCAPE THE BASEMENT: ";
        DoorController cell = LockedDoor("SeclusionRoomB");
        if (cell != null)
        {
            string id = cell.DoorID; string key = cell.RequiredKey;
            Add("Basement_Cell", B + "find a key to get out of this cell", () => DoorOpened(cell, id) || KeyTaken(key));
        }
        if (KeyExists("Morgue Key"))
        {
            DoorController morgue = LockedDoor("Morgue", "Morgue Key"); string mid = morgue != null ? morgue.DoorID : null;
            Add("Basement_MorgueKey", B + "find the key to the Morgue", () => KeyTaken("Morgue Key") || (morgue != null && DoorOpened(morgue, mid)));
        }
        ElectricTrap barrier = InRoom<ElectricTrap>("Morgue");
        if (barrier != null)
        {
            string flag = "TrapDisabled_" + IdOf(barrier.transform);
            Add("Barrier_Morgue", B + "shut down the electric barrier in the Morgue", () => Flag(flag));
        }
        if (KeyExists("blue key"))
        {
            DoorController machine = LockedDoor("MachineRoom", "blue key"); string mid = machine != null ? machine.DoorID : null;
            Add("Basement_BlueKey", B + "search the morgue drawers for a key", () => KeyTaken("blue key") || (machine != null && DoorOpened(machine, mid)));
        }
        GeneratorPuzzle gen = InRoom<GeneratorPuzzle>("MachineRoom");
        if (gen != null)
        {
            GeneratorPuzzle g = gen;
            Add("Generator_Basement", B + "find the crank handle and start the generator", () => g != null && g.isGeneratorOn);
        }
        if (cell != null || gen != null)
        {
            Add("Basement_Leave", B + "the way is open - get back upstairs!", () =>
            {
                if (player == null) return false;
                return player.position.y > -6f;   // the basement floor is ~17 m below the ground floor
            });
        }

        // ---------------------------------------------------------------- 3. room by room upstairs
        if (KeyExists("surgery key"))
            Add("SurgeryKey", "Guard Room: take the Surgery key", () => KeyTaken("surgery key"));

        DeadBodyPuzzle body = null;
        foreach (DeadBodyPuzzle b in FindObjectsByType<DeadBodyPuzzle>(inc)) if (b.isCorrectBody) { body = b; break; }
        if (body != null)
        {
            DeadBodyPuzzle b = body;
            Add("Body_Surgery", "Surgery: find a scalpel and cut open the right body", () => b != null && b.IsSolved);
        }

        CrankGeneratorPuzzle xray = InRoom<CrankGeneratorPuzzle>("ExamRoom");
        if (xray != null)
        {
            CrankGeneratorPuzzle x = xray;
            Add("Crank_Xray", "Exam Room: power the X-ray machine with the rusty crank", () => x != null && x.IsSolved);
        }
        // the Exam Room cabinet key opens the ground-floor Office ("office key"); the Classroom key opens the Head Office
        // (old scenes: "pharmacy key" / "office key")
        bool renamed = KeyExists("head office key");
        string cabinetKey = renamed ? "office key" : "pharmacy key";
        string classroomKey = renamed ? "head office key" : "office key";
        if (KeyExists(cabinetKey))
            Add("PharmacyKey", "Exam Room: open the locked cabinet and take the Office key", () => KeyTaken(cabinetKey));

        KeypadController kitchenPad = InRoom<KeypadController>("Kitchen");
        if (kitchenPad != null)
        {
            string flag = "Keypad_" + IdOf(kitchenPad.transform);
            Add("Kitchen_Code", "Kitchen (first floor): crack the door code", () => Flag(flag));
        }
        MicrowavePuzzle micro = InRoom<MicrowavePuzzle>("Kitchen");
        if (micro != null)
        {
            MicrowavePuzzle m = micro;
            Add("Microwave_Kitchen", "Kitchen: melt the ice in the microwave", () => m != null && m.IsSolved);
        }
        if (KeyExists("dining room key"))
            Add("Phone_Visit", "Visiting Room: answer the phone and take the Dining Room key", () => KeyTaken("dining room key"));
        // the Dining Room key opens the ground-floor Kitchen ("kitchen key"; older scenes called it "stock room key")
        string kitchenKey = KeyExists("kitchen key") ? "kitchen key" : "stock room key";
        if (KeyExists(kitchenKey))
        {
            DoorController kd = LockedDoor("KitchenA", kitchenKey); string kid = kd != null ? kd.DoorID : null;
            Add("StockKey", "Dining Room: find the Kitchen key", () => KeyTaken(kitchenKey) || (kd != null && DoorOpened(kd, kid)));
        }

        ToxicGasTrap gas = InRoom<ToxicGasTrap>("KitchenA");
        FuseBoxPuzzle kitchenFuses = InRoom<FuseBoxPuzzle>("KitchenA");
        if (gas != null || kitchenFuses != null)
        {
            string flag = gas != null ? "GasTrapCleared_" + IdOf(gas.transform) : null;
            FuseBoxPuzzle f = kitchenFuses;
            Add("Gas_KitchenA", "Kitchen (ground floor): find 2 fuses and fix the fuse box before the gas kills you",
                () => (f != null && f.IsSolved) || (flag != null && Flag(flag)));
        }
        OvenPuzzleManager oven = InRoom<OvenPuzzleManager>("KitchenA");
        if (oven != null)
        {
            OvenPuzzleManager ov = oven;
            Add("Oven_KitchenA", "Kitchen: set the three ovens to the right temperatures", () => ov != null && ov.IsSolved);
        }
        if (KeyExists(classroomKey))
            Add("OfficeKey", "Classroom: find the Head Office key", () => KeyTaken(classroomKey));

        GrandfatherClockPuzzle clock = InRoom<GrandfatherClockPuzzle>("Office");
        if (clock != null)
        {
            GrandfatherClockPuzzle c = clock;
            Add("Clock_Office", "Head Office: repair the grandfather clock", () => c != null && c.IsSolved);
        }
        FuseBoxPuzzle bathBox = InRoom<FuseBoxPuzzle>("Bathroom");
        if (bathBox != null)
        {
            FuseBoxPuzzle f = bathBox; string fuseName = f.fuseItemName;
            // the body bags lie in the STOCK ROOM, which is locked; its key is down in the basement (a note in the Kitchen says where)
            DoorController sd = LockedDoor("Stock", "stock room key"); string sid = sd != null ? sd.DoorID : null;
            if (sd != null && KeyExists("stock room key"))
                Add("StockRoomKey", "Stock Room: find the Stock Room key", () => KeyTaken("stock room key") || DoorOpened(sd, sid) || (f != null && f.IsSolved));
            if (KeyExists(fuseName))
                Add("BathFuse", "Stock Room: search the body bags for a fuse", () => KeyTaken(fuseName) || (f != null && f.IsSolved));
            Add("Fuse_Bathroom", "Bathroom: put the fuse in the control box", () => f != null && f.IsSolved);
        }
        AcidDrainPuzzle acid = InRoom<AcidDrainPuzzle>("Bathroom");
        if (acid != null)
        {
            AcidDrainPuzzle a = acid;
            // the valve is in the Gatehouse, which is locked; its key lies in the basement (Seclusion Room A)
            DoorController gh = LockedDoor("Gatehouse", "gatehouse key"); string ghid = gh != null ? gh.DoorID : null;
            if (gh != null && KeyExists("gatehouse key"))
                Add("GatehouseKey", "Gatehouse: find the Gatehouse key", () => KeyTaken("gatehouse key") || DoorOpened(gh, ghid) || (a != null && a.IsSolved));
            Add("Acid_Bathroom", "Bathroom: find the valve and drain the acid bath", () => a != null && a.IsSolved);
        }

        // ---------------------------------------------------------------- 4. the end
        Objective esc = Add("Escape", "Main gate: find the Main Gate Key and the 8-digit code, then escape",
            () => SaveManager.instance != null && SaveManager.instance.HasEvent(DonePrefix + "Escape"));
        esc.progressText = MainGateLock.ProgressText;

        // silent trackers for the achievements (Codebreaker, Let There Be Light)
        keypadFlags.Clear();
        foreach (KeypadController k in FindObjectsByType<KeypadController>(inc)) keypadFlags.Add("Keypad_" + IdOf(k.transform));
        hasHeadlamp = FindObjectsByType<HeadlampPickup>(inc).Length > 0;

        LastKnownTotal = objectives.Count;
        built = true;
    }

    // ============================================================================================ polling
    void Update()
    {
        if (!built || Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.5f;
        RefreshStates(true);
        GameProgress.CheckAchievements();
    }

    private void RefreshStates(bool notify)
    {
        SaveManager sm = SaveManager.instance;
        if (sm == null) return;

        if (player == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        // completion, IN ORDER: the active step is checked, and if it is already true (the player did it early)
        // the chain moves on at once. Done steps are sticky (saved), even if the key is used up later.
        foreach (Objective o in objectives)
        {
            if (!o.done) o.done = sm.HasEvent(DonePrefix + o.id);
            if (o.done) continue;
            if (o.id == "Escape") break;
            bool solved = false;
            try { solved = o.isDone != null && o.isDone(); } catch { solved = false; }
            if (!solved) break;                       // this is the step the player has to do now
            o.done = true;
            sm.RecordEvent(DonePrefix + o.id, false); // false = adaptive AI (DDA) timing stays exactly as before
            if (notify) GameUI.Toast("OBJECTIVE COMPLETE", o.title);
        }

        // the active step = the first one that is not done yet; everything up to it is "discovered" (journal)
        currentIndex = objectives.Count;
        for (int i = 0; i < objectives.Count; i++) if (!objectives[i].done) { currentIndex = i; break; }
        for (int i = 0; i < objectives.Count; i++)
        {
            Objective o = objectives[i];
            o.discovered = o.done || i <= currentIndex;
            if (o.discovered && !sm.HasEvent(FoundPrefix + o.id)) sm.RecordEvent(FoundPrefix + o.id, false);
        }

        if (notify && currentIndex != lastAnnouncedIndex)
        {
            lastAnnouncedIndex = currentIndex;
            Objective cur = Current();
            if (cur != null) GameUI.Toast("NEW OBJECTIVE", cur.title);
        }

        // achievements that are not steps of the chain
        if (hasHeadlamp && !sm.HasEvent(DonePrefix + "Headlamp") && Flag(HeadlampPickup.SaveFlag)) sm.RecordEvent(DonePrefix + "Headlamp", false);
        if (keypadFlags.Count > 0 && !sm.HasEvent(DonePrefix + "Keypads"))
        {
            bool all = true;
            foreach (string f in keypadFlags) if (!Flag(f)) { all = false; break; }
            if (all) sm.RecordEvent(DonePrefix + "Keypads", false);
        }
    }

    /// <summary>Called by the ending: the final objective is done.</summary>
    public static void MarkEscaped()
    {
        if (SaveManager.instance != null) SaveManager.instance.RecordEvent(DonePrefix + "Escape", false);
    }

    // ============================================================================================ queries for the UI
    public string TitleWithProgress(Objective o)
    {
        string p = o.progressText != null ? o.progressText() : "";
        return string.IsNullOrEmpty(p) ? o.title : o.title + " " + p;
    }

    /// <summary>The objective shown on the HUD: always the next step of the chain.</summary>
    public Objective Current()
    {
        if (!built || currentIndex < 0 || currentIndex >= objectives.Count) return null;
        return objectives[currentIndex];
    }

    public int DoneCount() { int n = 0; foreach (Objective o in objectives) if (o.done) n++; return n; }
    public int UndiscoveredCount() { int n = 0; foreach (Objective o in objectives) if (!o.discovered) n++; return n; }
}
