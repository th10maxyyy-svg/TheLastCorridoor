using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OBJECTIVES (FR_48, FR_49, FR_31).
/// Builds the objective list automatically from the puzzles and hazards that exist in the scene - no setup needed.
///  - An objective is DISCOVERED when the player first comes near its puzzle (so the list grows as the asylum is explored).
///  - It is COMPLETED automatically when the puzzle reports it is solved (or its save flag is set).
/// Both states are stored as events in the save file, so they survive deaths, respawns and loading.
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
        public System.Func<string> progressText;   // optional "(3/5)"
        public bool discovered;
        public bool done;
    }

    private readonly List<Objective> objectives = new List<Objective>();
    public IReadOnlyList<Objective> Objectives => objectives;

    [SerializeField] private float discoverDistance = 7f;
    private float nextPoll;
    private Transform player;
    private bool built;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; LastKnownTotal = 0; }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        Build();
    }

    // ------------------------------------------------------------------------------------------ building the list
    private static string IdOf(Transform t) => SaveManager.GetSceneObjectId(t);

    private static bool Flag(string flag) => SaveManager.instance != null && SaveManager.instance.HasFlag(flag);

    private Objective Add(string id, string title, int order, Transform anchor, System.Func<bool> isDone, bool alwaysVisible = false)
    {
        Objective o = new Objective { id = id, title = title, order = order, isDone = isDone, alwaysVisible = alwaysVisible };
        if (anchor != null) o.anchors.Add(anchor);
        objectives.Add(o);
        return o;
    }

    private void Build()
    {
        objectives.Clear();
        FindObjectsInactive inc = FindObjectsInactive.Include;

        foreach (HeadlampPickup h in FindObjectsByType<HeadlampPickup>(inc))
        {
            Add("Headlamp", "Find a light source", 0, h.transform, () => Flag(HeadlampPickup.SaveFlag), true);
            break;
        }

        foreach (GrandfatherClockPuzzle p in FindObjectsByType<GrandfatherClockPuzzle>(inc))
        {
            GrandfatherClockPuzzle c = p;
            Add("Clock_" + IdOf(c.transform), "Repair the grandfather clock", 10, c.transform, () => c != null && c.IsSolved);
        }

        foreach (OvenPuzzleManager p in FindObjectsByType<OvenPuzzleManager>(inc))
        {
            OvenPuzzleManager m = p;
            Objective o = Add("Oven_" + IdOf(m.transform), "Set the three ovens to the right temperatures", 20, null, () => m != null && m.IsSolved);
            foreach (OvenDial d in FindObjectsByType<OvenDial>(inc)) o.anchors.Add(d.transform);
            if (o.anchors.Count == 0) o.anchors.Add(m.transform);
        }

        foreach (MicrowavePuzzle p in FindObjectsByType<MicrowavePuzzle>(inc))
        {
            MicrowavePuzzle m = p;
            Add("Microwave_" + IdOf(m.transform), "Get the microwave working", 20, m.transform, () => m != null && m.IsSolved);
        }

        List<DeadBodyPuzzle> bodies = new List<DeadBodyPuzzle>(FindObjectsByType<DeadBodyPuzzle>(inc));
        DeadBodyPuzzle rightBody = bodies.Find(b => b.isCorrectBody);
        if (rightBody != null)
        {
            DeadBodyPuzzle b = rightBody;
            Objective o = Add("Body_" + IdOf(b.transform), "Find the right body and cut it open", 30, null, () => b != null && b.IsSolved);
            foreach (DeadBodyPuzzle any in bodies) o.anchors.Add(any.transform);
        }

        foreach (CrankGeneratorPuzzle p in FindObjectsByType<CrankGeneratorPuzzle>(inc))
        {
            CrankGeneratorPuzzle c = p;
            Add("Crank_" + IdOf(c.transform), "Power the X-ray machine with a crank", 40, c.transform, () => c != null && c.IsSolved);
        }

        foreach (GeneratorPuzzle p in FindObjectsByType<GeneratorPuzzle>(inc))
        {
            GeneratorPuzzle g = p;
            Add("Generator_" + IdOf(g.transform), "Start the generator", 40, g.transform, () => g != null && g.isGeneratorOn);
        }

        foreach (FuseBoxPuzzle p in FindObjectsByType<FuseBoxPuzzle>(inc))
        {
            FuseBoxPuzzle f = p;
            string title = "Restore the fuse box (find " + f.requiredFuses + " x " + f.fuseItemName + ")";
            Add("Fuse_" + IdOf(f.transform), title, 50, f.transform, () => f != null && f.IsSolved);
        }

        foreach (AcidDrainPuzzle p in FindObjectsByType<AcidDrainPuzzle>(inc))
        {
            AcidDrainPuzzle a = p;
            Add("Acid_" + IdOf(a.transform), "Drain the acid tank to reach the key", 60, a.transform, () => a != null && a.IsSolved);
        }

        foreach (ElectricTrap p in FindObjectsByType<ElectricTrap>(inc))
        {
            string flag = "TrapDisabled_" + IdOf(p.transform);
            Add("Barrier_" + IdOf(p.transform), "Shut down the electrified barrier", 60, p.transform, () => Flag(flag));
        }

        foreach (ToxicGasTrap p in FindObjectsByType<ToxicGasTrap>(inc))
        {
            string flag = "GasTrapCleared_" + IdOf(p.transform);
            Add("Gas_" + IdOf(p.transform), "Survive the gas room - find the ventilation switch", 60, p.transform, () => Flag(flag));
        }

        KeypadController[] pads = FindObjectsByType<KeypadController>(inc);
        if (pads.Length > 0)
        {
            List<string> padFlags = new List<string>();
            foreach (KeypadController k in pads) padFlags.Add("Keypad_" + IdOf(k.transform));
            System.Func<int> padsDone = () => { int n = 0; foreach (string f in padFlags) if (Flag(f)) n++; return n; };
            Objective o = Add("Keypads", "Crack the keypad codes", 70, null, () => padsDone() >= padFlags.Count);
            foreach (KeypadController k in pads) o.anchors.Add(k.transform);
            o.progressText = () => "(" + padsDone() + "/" + padFlags.Count + ")";
        }

        if (GameProgress.CountNotesInScene() > 0)
        {
            Objective o = Add("Notes", "Read the notes to learn what happened here", 5, null,
                () => GameProgress.NotesRead() >= GameProgress.CountNotesInScene(), true);
            o.progressText = () => "(" + GameProgress.NotesRead() + "/" + GameProgress.CountNotesInScene() + ")";
        }

        Add("Escape", "Escape the asylum through the main gate", 100, null,
            () => SaveManager.instance != null && SaveManager.instance.HasEvent(DonePrefix + "Escape"), true);

        objectives.Sort((a, b) => a.order.CompareTo(b.order));
        LastKnownTotal = objectives.Count;
        built = true;
        RefreshStates(false);
    }

    // ------------------------------------------------------------------------------------------ polling
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

        foreach (Objective o in objectives)
        {
            o.discovered = o.alwaysVisible || sm.HasEvent(FoundPrefix + o.id);
            o.done = sm.HasEvent(DonePrefix + o.id);

            // discovery: the player walked close to the puzzle
            if (!o.discovered && player != null)
            {
                foreach (Transform a in o.anchors)
                {
                    if (a == null) continue;
                    Vector3 delta = a.position - player.position;
                    if (Mathf.Abs(delta.y) < 4f && new Vector2(delta.x, delta.z).magnitude < discoverDistance)
                    {
                        o.discovered = true;
                        sm.RecordEvent(FoundPrefix + o.id, false);
                        if (notify && !o.done) GameUI.Toast("NEW OBJECTIVE", o.title);
                        break;
                    }
                }
            }

            // completion: the puzzle says it is solved
            if (!o.done && o.id != "Escape")
            {
                bool solved = false;
                try { solved = o.isDone != null && o.isDone(); } catch { solved = false; }
                if (solved)
                {
                    o.done = true;
                    if (!o.discovered) { o.discovered = true; sm.RecordEvent(FoundPrefix + o.id, false); }
                    sm.RecordEvent(DonePrefix + o.id, false); // false = adaptive AI (DDA) timing stays exactly as before
                    if (notify) GameUI.Toast("OBJECTIVE COMPLETE", o.title);
                }
            }
        }
    }

    /// <summary>Called by the ending: the final objective is done.</summary>
    public static void MarkEscaped()
    {
        if (SaveManager.instance != null) SaveManager.instance.RecordEvent(DonePrefix + "Escape", false);
    }

    // ------------------------------------------------------------------------------------------ queries for the UI
    public string TitleWithProgress(Objective o)
    {
        string p = o.progressText != null ? " " + o.progressText() : "";
        return o.title + p;
    }

    /// <summary>The objective shown on the HUD: the most recently discovered one that is not finished yet.</summary>
    public Objective Current()
    {
        SaveManager sm = SaveManager.instance;
        Objective best = null;
        int bestIndex = -1;
        if (sm != null)
        {
            List<string> events = SaveManager.SplitList(sm.currentSaveData.eventsCSV);
            foreach (Objective o in objectives)
            {
                if (o.done || !o.discovered || o.alwaysVisible) continue;
                int idx = events.LastIndexOf(FoundPrefix + o.id);
                if (idx > bestIndex) { bestIndex = idx; best = o; }
            }
        }
        if (best != null) return best;

        // nothing new: first unfinished always-visible objective, the escape last
        foreach (Objective o in objectives) if (!o.done && o.alwaysVisible && o.id != "Escape" && o.id != "Notes") return o;
        foreach (Objective o in objectives) if (!o.done && o.id == "Escape") return o;
        return null;
    }

    public int DoneCount() { int n = 0; foreach (Objective o in objectives) if (o.done) n++; return n; }
    public int UndiscoveredCount() { int n = 0; foreach (Objective o in objectives) if (!o.discovered) n++; return n; }
}
