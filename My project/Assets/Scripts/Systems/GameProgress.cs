using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// PROGRESSION, ACHIEVEMENTS, STATISTICS AND ENDINGS (FDS Section 6.8 - FR_48 to FR_56, FR_31, FR_51).
///
/// Gameplay scripts only make one-line reports here (note read, item picked up, player hid, taser fired).
/// Objectives are tracked by ObjectiveTracker, which stores completed objectives as events in the save file.
/// Achievements live in a separate PROFILE file (Achievements.json) so they survive new games and permadeath.
/// The ending is decided from what the player did during the run (notes read, deaths, taser use).
/// </summary>
public static class GameProgress
{
    // =============================================================================================== run reports
    public static void NoteRead(ReadableNote note)
    {
        if (note == null || SaveManager.instance == null) return;
        string id = SaveManager.GetSceneObjectId(note.transform);
        if (SaveManager.instance.RecordNoteRead(id))
        {
            int read = NotesRead(), total = Mathf.Max(read, CountNotesInScene());
            GameUI.Toast("NOTE ADDED TO JOURNAL", "Notes found: " + read + " / " + total + "   (TAB to re-read)");
            CheckAchievements();
        }
    }

    public static void ItemPickedUp(GameObject item)
    {
        if (item == null || SaveManager.instance == null) return;
        string name = DisplayNameOf(item);
        if (SaveManager.instance.RecordItemFound(name)) CheckAchievements();
    }

    public static void PlayerHid()
    {
        if (SaveManager.instance == null) return;
        SaveManager.instance.currentSaveData.timesHidden++;
        CheckAchievements();
    }

    public static void TaserFired()
    {
        if (SaveManager.instance == null) return;
        SaveManager.instance.currentSaveData.taserShots++;
        SaveManager.instance.SaveWorldState();
        CheckAchievements();
    }

    // =============================================================================================== hidden lore (FR_64)
    public class LoreEntry { public string id, title, text; }

    private static readonly string[] TagLore =
    {
        "TOE TAG - PATIENT 0112, \"Brother S.\"\nAdmitted for religious mania. Treatment: the Director's Protocol, stage 3.\nNote in red ink: DO NOT RELEASE. DO NOT LET HIM PRAY.",
        "TOE TAG - NURSE E. HALLOWAY\nCause of death: \"accident in Ward B\".\nSigned by the Director. The signature is not his usual hand - it shakes.",
        "TOE TAG - UNKNOWN MALE\nFound in the dining room after the cook was locked upstairs.\nThe bite marks on the arm are not human.",
        "TOE TAG - DR. M. REYES\nShe tried to stop the Protocol and wrote to the city.\nFiled as \"suicide\". Her letters were never sent.",
        "TOE TAG - PATIENT 0113\nSurvived stage 3 for nine days. Kept asking for \"Brother\" in his sleep.\nOn the ninth night, Brother answered.",
        "A SCRAP OF PAPER UNDER A TAG\n\"The Protocol does not cure them. It empties them - and something else moves in.\nIf you are reading this, the gates are already locked.\"  - E.H.",
        "TOE TAG - THE DIRECTOR?\nThe name has been scratched out with a fingernail.\nThe drawer is empty. The tag is still warm.",
    };

    private const string PhoneLore =
        "VOICEMAIL - NIGHT SHIFT\n\"...this is the night shift. The Director sealed the morgue gates before midnight. Whatever was in Ward B is walking the corridors now.\nIf anyone hears this - don't hide in the same place twice. He learns. He always learns...\" [static]";

    /// <summary>All hidden lore that exists in this scene, in a stable order (so the same drawer always holds the same tag).</summary>
    public static List<LoreEntry> AllLoreInScene()
    {
        List<LoreEntry> list = new List<LoreEntry>();
        List<string> drawerIds = new List<string>();
        foreach (MorgueBox m in Object.FindObjectsByType<MorgueBox>(FindObjectsInactive.Include))
            if (m.HoldsLore) drawerIds.Add(SaveManager.GetSceneObjectId(m.transform));
        drawerIds.Sort(System.StringComparer.Ordinal);
        for (int i = 0; i < drawerIds.Count; i++)
            list.Add(new LoreEntry { id = "Lore_" + drawerIds[i], title = "Toe tag " + (i + 1), text = TagLore[i % TagLore.Length] });

        foreach (RingingPhone p in Object.FindObjectsByType<RingingPhone>(FindObjectsInactive.Include))
            list.Add(new LoreEntry { id = "Lore_" + SaveManager.GetSceneObjectId(p.transform), title = "Voicemail", text = PhoneLore });
        return list;
    }

    public static int LoreFoundCount() => CurrentSave != null ? SaveManager.CountList(CurrentSave.loreFoundCSV) : 0;

    public static void LoreFound(Component source, PlayerStats player)
    {
        if (source == null || SaveManager.instance == null) return;
        string id = "Lore_" + SaveManager.GetSceneObjectId(source.transform);
        LoreEntry entry = AllLoreInScene().Find(e => e.id == id);
        if (entry == null) return;

        bool first = SaveManager.instance.RecordLoreFound(id);
        if (player != null) InteractionRaycast.Notify(player, entry.text, 7f);
        if (first)
        {
            GameUI.Toast("HIDDEN LORE FOUND", entry.title + "   (" + LoreFoundCount() + " / " + AllLoreInScene().Count + ")  - TAB to re-read");
            CheckAchievements();
        }
    }

    // =============================================================================================== helpers
    public static SaveData CurrentSave => SaveManager.instance != null ? SaveManager.instance.currentSaveData : null;

    public static string DisplayNameOf(GameObject item)
    {
        if (item == null) return "";
        KeyItem key = item.GetComponent<KeyItem>();
        string name = key != null ? key.GetKeyName() : item.name;
        int open = name.LastIndexOf(" (");
        if (open > 0 && name.EndsWith(")")) name = name.Substring(0, open);
        return name.Replace("(Clone)", "").Trim();
    }

    public static int NotesRead() => CurrentSave != null ? SaveManager.CountList(CurrentSave.notesReadCSV) : 0;

    private static int cachedNoteCount = -1;
    private static int cachedNoteFrame = -1;

    public static int CountNotesInScene()
    {
        if (cachedNoteFrame == Time.frameCount) return cachedNoteCount;
        cachedNoteFrame = Time.frameCount;
        cachedNoteCount = Object.FindObjectsByType<ReadableNote>(FindObjectsInactive.Include).Length;
        if (cachedNoteCount > 0) PlayerPrefs.SetInt("TLC_NotesTotal", cachedNoteCount);
        else cachedNoteCount = PlayerPrefs.GetInt("TLC_NotesTotal", 0);
        return cachedNoteCount;
    }

    public static int CountSolvedObjectives(SaveData d)
    {
        if (d == null) return 0;
        int n = 0;
        foreach (string e in SaveManager.SplitList(d.eventsCSV)) if (e.StartsWith(ObjectiveTracker.DonePrefix)) n++;
        return n;
    }

    public static bool AnyEventStartsWith(SaveData d, string prefix)
    {
        if (d == null) return false;
        foreach (string e in SaveManager.SplitList(d.eventsCSV)) if (e.StartsWith(prefix)) return true;
        return false;
    }

    public static int Deaths(SaveData d) => d == null ? 0 : Mathf.Clamp(3 - d.lives, 0, 3);

    // =============================================================================================== achievements
    public class AchievementDef
    {
        public string id, title, description;
        public bool hidden;
        public System.Func<SaveData, bool> condition;   // checked during play (null = only unlocked by code, e.g. endings)
        public System.Func<SaveData, int> progress;     // optional progress counter
        public int target;
    }

    [System.Serializable]
    private class ProfileData
    {
        public string unlockedCSV = "";
        public string unlockDatesCSV = "";   // "id=yyyy-MM-dd;"
        public string endingsSeenCSV = "";
        public int gamesFinished = 0;
        public float bestEscapeTime = 0f;
    }

    private static ProfileData profile;
    private static readonly List<AchievementDef> defs = new List<AchievementDef>();
    private static readonly List<string> unlockedThisRun = new List<string>();
    private static string ProfilePath => Path.Combine(Application.persistentDataPath, "Achievements.json");

    public static IReadOnlyList<AchievementDef> Achievements { get { EnsureDefs(); return defs; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        profile = null;
        unlockedThisRun.Clear();
        cachedNoteFrame = -1;
    }

    private static AchievementDef Obj(string id, string title, string desc, string objectivePrefix)
    {
        return new AchievementDef
        {
            id = id, title = title, description = desc,
            condition = d => AnyEventStartsWith(d, ObjectiveTracker.DonePrefix + objectivePrefix)
        };
    }

    private static void EnsureDefs()
    {
        if (defs.Count > 0) return;
        defs.Add(Obj("LIGHT", "Let There Be Light", "Find the headlamp.", "Headlamp"));
        defs.Add(Obj("CLOCK", "Out of Time", "Solve the grandfather clock puzzle.", "Clock"));
        defs.Add(Obj("OVEN", "Kitchen Nightmare", "Set the three ovens to the right temperatures.", "Oven"));
        defs.Add(Obj("MICROWAVE", "Dinner Is Served", "Get the microwave working.", "Microwave"));
        defs.Add(Obj("SURGEON", "Amateur Surgeon", "Find the right body and cut it open.", "Body"));
        defs.Add(Obj("XRAY", "Hand-Cranked", "Power the X-ray machine with the crank.", "Crank"));
        defs.Add(Obj("GENERATOR", "Let It Run", "Start the generator.", "Generator"));
        defs.Add(Obj("FUSES", "Rewired", "Restore a fuse box.", "Fuse"));
        defs.Add(Obj("ACID", "Acid Test", "Drain the acid tank and take the key.", "Acid"));
        defs.Add(Obj("BARRIER", "Grounded", "Shut down the electrified barrier.", "Barrier"));
        defs.Add(Obj("GAS", "Breathe Again", "Escape the toxic gas room.", "Gas"));
        defs.Add(new AchievementDef
        {
            id = "CODES", title = "Codebreaker", description = "Crack every keypad code.",
            condition = d => AnyEventStartsWith(d, ObjectiveTracker.DonePrefix + "Keypads")
        });
        defs.Add(new AchievementDef
        {
            id = "ARCHIVIST", title = "Archivist", description = "Read every note in the asylum.",
            condition = d => d != null && CountNotesInScene() > 0 && SaveManager.CountList(d.notesReadCSV) >= CountNotesInScene(),
            progress = d => d != null ? SaveManager.CountList(d.notesReadCSV) : 0,
            target = -1 // = number of notes in the scene
        });
        defs.Add(new AchievementDef
        {
            id = "LORE", title = "Dig Deeper", description = "Uncover every piece of hidden lore.", hidden = true,
            condition = d => d != null && SaveManager.CountList(d.loreFoundCSV) > 0 && SaveManager.CountList(d.loreFoundCSV) >= AllLoreInScene().Count
        });
        defs.Add(new AchievementDef
        {
            id = "SHADOW", title = "Hide and Seek", description = "Hide from him 10 times.",
            condition = d => d != null && d.timesHidden >= 10, progress = d => d != null ? d.timesHidden : 0, target = 10
        });
        defs.Add(new AchievementDef
        {
            id = "COLLECTOR", title = "Scavenger", description = "Pick up 10 different items.",
            condition = d => d != null && SaveManager.CountList(d.itemsFoundCSV) >= 10,
            progress = d => d != null ? SaveManager.CountList(d.itemsFoundCSV) : 0, target = 10
        });
        defs.Add(new AchievementDef
        {
            id = "SHOCK", title = "Shock Therapy", description = "Fire the taser.",
            condition = d => d != null && d.taserShots > 0
        });
        defs.Add(new AchievementDef { id = "DAYLIGHT", title = "Daylight", description = "Escape the asylum." });
        defs.Add(new AchievementDef { id = "UNTOUCHED", title = "Untouched", description = "Escape without dying once." });
        defs.Add(new AchievementDef { id = "GHOST", title = "Ghost", description = "Get the GHOST ending.", hidden = true });
        defs.Add(new AchievementDef { id = "TRUTH", title = "The Hidden Truth", description = "Get the HIDDEN TRUTH ending.", hidden = true });
        defs.Add(new AchievementDef { id = "CONSUMED", title = "Consumed", description = "Lose all three lives.", hidden = true });
        defs.Add(new AchievementDef { id = "SPEED", title = "Run, Rabbit", description = "Escape in under 30 minutes.", hidden = true });
    }

    private static void EnsureProfile()
    {
        if (profile != null) return;
        profile = null;
        try
        {
            if (File.Exists(ProfilePath)) profile = JsonUtility.FromJson<ProfileData>(File.ReadAllText(ProfilePath));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("GameProgress: achievements file unreadable, starting a new one (" + e.Message + ")");
            profile = null;
        }
        if (profile == null) profile = new ProfileData();
        if (profile.unlockedCSV == null) profile.unlockedCSV = "";
        if (profile.unlockDatesCSV == null) profile.unlockDatesCSV = "";
        if (profile.endingsSeenCSV == null) profile.endingsSeenCSV = "";
    }

    private static void SaveProfile()
    {
        try
        {
            string tmp = ProfilePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(profile, true));
            if (File.Exists(ProfilePath)) File.Delete(ProfilePath);
            File.Move(tmp, ProfilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("GameProgress: could not save achievements (" + e.Message + ")");
        }
    }

    private static bool CsvHas(string csv, string v)
    {
        foreach (string s in SaveManager.SplitList(csv)) if (s == v) return true;
        return false;
    }

    public static bool IsUnlocked(string id)
    {
        EnsureProfile();
        return CsvHas(profile.unlockedCSV, id);
    }

    public static string UnlockDate(string id)
    {
        EnsureProfile();
        foreach (string e in profile.unlockDatesCSV.Split(';'))
        {
            int eq = e.IndexOf('=');
            if (eq > 0 && e.Substring(0, eq) == id) return e.Substring(eq + 1);
        }
        return "";
    }

    public static int UnlockedCount()
    {
        EnsureDefs(); EnsureProfile();
        int n = 0;
        foreach (AchievementDef d in defs) if (CsvHas(profile.unlockedCSV, d.id)) n++;
        return n;
    }

    public static int TargetOf(AchievementDef def) => def.target < 0 ? Mathf.Max(1, CountNotesInScene()) : def.target;

    /// <summary>Unlocks an achievement (FR_52). Shows a pop-up the first time.</summary>
    public static void Unlock(string id)
    {
        EnsureDefs(); EnsureProfile();
        if (CsvHas(profile.unlockedCSV, id)) return;
        AchievementDef def = defs.Find(a => a.id == id);
        if (def == null) return;

        profile.unlockedCSV += id + ",";
        profile.unlockDatesCSV += id + "=" + System.DateTime.Now.ToString("yyyy-MM-dd") + ";";
        SaveProfile();
        unlockedThisRun.Add(id);
        GameUI.Toast("ACHIEVEMENT UNLOCKED", def.title + " - " + def.description, true);
        Debug.Log("[Achievement] " + def.title);
    }

    /// <summary>Checks every play-time achievement against the current save. Cheap - called on events and twice a second.</summary>
    public static void CheckAchievements()
    {
        SaveData d = CurrentSave;
        if (d == null) return;
        EnsureDefs(); EnsureProfile();
        foreach (AchievementDef def in defs)
        {
            if (def.condition == null || CsvHas(profile.unlockedCSV, def.id)) continue;
            bool ok = false;
            try { ok = def.condition(d); } catch { ok = false; }
            if (ok) Unlock(def.id);
        }
    }

    public static List<string> TakeUnlockedThisRun()
    {
        List<string> list = new List<string>(unlockedThisRun);
        unlockedThisRun.Clear();
        return list;
    }

    public static int EndingsSeen()
    {
        EnsureProfile();
        return SaveManager.CountList(profile.endingsSeenCSV);
    }

    public static bool EndingSeen(EndingType e)
    {
        EnsureProfile();
        return CsvHas(profile.endingsSeenCSV, e.ToString());
    }

    // =============================================================================================== endings (FR_54 - FR_56)
    public enum EndingType { Escape, Ghost, HiddenTruth, Consumed }
    public const int EndingCount = 4;

    public class RunSummary
    {
        public EndingType ending;
        public string title, tagline, description, reason;
        public float playTime;
        public int deaths, notesRead, notesTotal, objectivesDone, objectivesTotal, doorsUnlocked, timesHidden, itemsFound, taserShots;
        public string skillClass;
        public int aggression;
        public int achievementsUnlocked, achievementsTotal, endingsSeen;
        public List<string> newAchievements = new List<string>();
    }

    /// <summary>
    /// Decides the ending from the player's choices during the run (FR_55) and records it.
    /// MUST be called before SaveManager.DeleteSave(), because the run statistics live in the save.
    /// </summary>
    public static RunSummary FinishRun(bool escaped)
    {
        SaveData d = CurrentSave ?? new SaveData();
        EnsureDefs(); EnsureProfile();

        RunSummary s = new RunSummary();
        s.playTime = d.playTime;
        s.deaths = escaped ? Deaths(d) : 3;
        s.notesRead = SaveManager.CountList(d.notesReadCSV);
        s.notesTotal = Mathf.Max(s.notesRead, CountNotesInScene());
        s.objectivesDone = CountSolvedObjectives(d) + (escaped ? 1 : 0);
        s.objectivesTotal = Mathf.Max(s.objectivesDone, ObjectiveTracker.LastKnownTotal);
        s.doorsUnlocked = SaveManager.CountList(d.unlockedDoorsCSV);
        s.timesHidden = d.timesHidden;
        s.itemsFound = SaveManager.CountList(d.itemsFoundCSV);
        s.taserShots = d.taserShots;
        s.skillClass = string.IsNullOrEmpty(d.skillClass) ? "Balanced" : d.skillClass;
        s.aggression = d.aggressionLevel;

        bool allNotes = s.notesTotal > 0 && s.notesRead >= s.notesTotal;
        if (!escaped)
        {
            s.ending = EndingType.Consumed;
            s.title = "ENDING: CONSUMED";
            s.tagline = "The corridor keeps what it catches.";
            s.description = "Your third life ran out. The asylum has a new patient, and he will never be discharged.";
            s.reason = "You lost all three lives.";
        }
        else if (allNotes)
        {
            s.ending = EndingType.HiddenTruth;
            s.title = "ENDING: THE HIDDEN TRUTH";
            s.tagline = "You walk out knowing what really happened here.";
            s.description = "Every note, every warning, every confession - you pieced it together. The Director's experiments, the locked doors, the thing they created. You do not just escape. You leave with the truth, and the world will hear it.";
            s.reason = "You escaped AND read all " + s.notesTotal + " notes.";
        }
        else if (s.deaths == 0 && s.taserShots == 0)
        {
            s.ending = EndingType.Ghost;
            s.title = "ENDING: GHOST";
            s.tagline = "No one saw you leave. Not even him.";
            s.description = "You never fought back and you never fell. You moved like a shadow through his halls, and the asylum does not even know you were there.";
            s.reason = "You escaped without dying once and without using the taser.";
        }
        else
        {
            s.ending = EndingType.Escape;
            s.title = "ENDING: THE ESCAPE";
            s.tagline = "The Nightmare is over... for now.";
            s.description = "Sunlight. Air. You made it out - but you leave with more questions than answers. Somewhere behind you, in the dark, he is still waiting.";
            s.reason = "You escaped.  (Hidden endings: read every note, or escape without dying and without the taser.)";
        }

        // record the ending in the profile
        if (!CsvHas(profile.endingsSeenCSV, s.ending.ToString())) profile.endingsSeenCSV += s.ending.ToString() + ",";
        if (escaped)
        {
            profile.gamesFinished++;
            if (profile.bestEscapeTime <= 0f || d.playTime < profile.bestEscapeTime) profile.bestEscapeTime = d.playTime;
        }
        SaveProfile();

        // ending achievements
        CheckAchievements();
        if (escaped)
        {
            Unlock("DAYLIGHT");
            if (s.deaths == 0) Unlock("UNTOUCHED");
            if (d.playTime > 0f && d.playTime < 30f * 60f) Unlock("SPEED");
            if (s.ending == EndingType.Ghost) Unlock("GHOST");
            if (s.ending == EndingType.HiddenTruth) Unlock("TRUTH");
        }
        else Unlock("CONSUMED");

        s.newAchievements = TakeUnlockedThisRun();
        s.achievementsUnlocked = UnlockedCount();
        s.achievementsTotal = defs.Count;
        s.endingsSeen = EndingsSeen();
        return s;
    }

    // =============================================================================================== item descriptions (inspection, FR_35)
    private static Dictionary<string, string> itemInfo;

    public static string DescribeItem(string name)
    {
        if (itemInfo == null)
        {
            itemInfo = new Dictionary<string, string>
            {
                { "bathroom fuse", "A ceramic fuse pulled from the bathroom wiring. One of the fuse boxes is missing exactly this." },
                { "fuse", "A heavy industrial fuse. Fuse boxes in the asylum need these to bring the power back." },
                { "escapekey", "The key to the main gate. Freedom is on the other side of that lock." },
                { "exam room key", "A small brass key, sticky with blood. It opens the examination room." },
                { "morgue key", "Cold to the touch. It opens the morgue." },
                { "pendulum", "The missing pendulum of the grandfather clock. The clock will not run without it." },
                { "redkey", "A key painted red. The Director locked the Red Door with it." },
                { "rusty crank", "A rusted hand crank. Something in the asylum still has to be powered by hand." },
                { "crank handle", "A crank handle that fits a generator." },
                { "scalpel", "A surgical scalpel - sharp enough to cut through skin." },
                { "valve", "A valve handle. It would fit the pipe on the acid tank." },
                { "blue key", "A key with a faded blue tag." },
                { "class room key", "Opens the old classroom." },
                { "dining room key", "Opens the dining room. The note said: feed him." },
                { "guard key", "Taken from the guard room. It opens the guards' doors." },
                { "ice block", "A block of ice with something frozen inside. It needs heat." },
                { "office key", "Opens the office on the ground floor." },
                { "head office key", "Opens the Director's Head Office upstairs." },
                { "pharmacy key", "Opens the pharmacy, where the medicine was kept." },
                { "rusty key", "An old rusty key. Which lock is it for?" },
                { "stock room key", "Opens the stock room." },
                { "kitchen key", "Opens the kitchen on the ground floor. The note said: feed him." },
                { "gatehouse key", "The night guard's key to the gatehouse by the main gate." },
                { "surgery key", "Opens the surgery." },
                { "visit room key", "Opens the visiting room." },
            };
        }
        string key = (name ?? "").Trim().ToLowerInvariant();
        if (itemInfo.TryGetValue(key, out string text)) return text;
        if (key.Contains("key")) return "A key you found in the asylum. Somewhere there is a door that it opens.";
        return "Something you found in the asylum. It might be useful - or it might make noise if you throw it.";
    }
}
