using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public int lives = 3;
    public float[] playerPosition = new float[3];
    public float[] playerRotation = new float[4];

    // ID of the item currently in the player's hand ("" = empty hand)
    public string heldItemName = "";
    public string itemHomesCSV = "";          // where each item was first found (id|x|y|z;) - items come back here when the player dies holding them

    // Every item lying on the floor away from its original spot: "id|x|y|z;id|x|y|z;"
    public string droppedItemsCSV = "";

    // WORLD STATE PERSISTENCE (CSV strings because JsonUtility can't save dictionaries)
    public string unlockedDoorsCSV = "";
    public string collectedItemsCSV = "";
    public string flagsCSV = ""; // One-time events (headlamp picked, trap disabled, ambush played...)
    public bool isNightmareActive = false;

    // ADAPTIVE AI (FYP document Section 7.4) - kept across deaths/respawns
    public int aggressionLevel = 3;          // 1..5
    public string skillClass = "Balanced";   // Struggling / Balanced / Confident
    public float comfortScore = 60f;         // last computed comfort score (0..100)
    public float playTime = 0f;              // total seconds played
    public float lastProgressTime = 0f;      // playTime when the last puzzle/door/progress happened
    public float lastPuzzleDuration = -1f;   // seconds the last puzzle/progress step took (-1 = none yet)
    public int deathsAtLastProgress = 0;     // deaths count when the current section started
    public string hidingSpotsCSV = "";       // "spotId|count|x|y|z;" how often each hiding spot was used

    // PROGRESSION / STATS / ENDINGS (FR_31, FR_48-FR_56) - see GameProgress.cs
    public int saveVersion = 2;
    public string eventsCSV = "";            // completed objectives / puzzles / discovered objectives (ids)
    public string notesReadCSV = "";         // ids of the notes the player has read
    public string itemsFoundCSV = "";        // display names of every item the player has picked up at least once
    public int timesHidden = 0;
    public int taserShots = 0;
    public string savedAt = "";              // real-world time of the last write (shown in the save list)

    // FR_08 / FR_40 / FR_64
    public int variationSeed = 0;            // per-run seed for the session variation (0 = not chosen yet)
    public string loreFoundCSV = "";         // hidden lore the player has uncovered (morgue drawers, the phone call)
    public bool hasEnemyPosition = false;    // enemy position at the last safe checkpoint (restored on Resume / Load)
    public float[] enemyPosition = new float[3];
}

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    public static string SaveFilePath => Path.Combine(Application.persistentDataPath, "AsylumSave.json");

    public SaveData currentSaveData = new SaveData();

    // Item registry: har item ko scene-load par ek unique ID milti hai (naam + asal position),
    // taake ek jaise naam wale items (jaise do "Fuse") aapas mein gadbad na hon.
    private readonly Dictionary<GameObject, string> itemIds = new Dictionary<GameObject, string>();
    private readonly Dictionary<string, GameObject> itemsById = new Dictionary<string, GameObject>();
    private int registrySceneLoad = -1;

    // Dropped item positions are only refreshed after RespawnManager has put them back,
    // otherwise a save during scene load would overwrite them with their original positions.
    private int restoredSceneLoad = -1;
    private int sceneLoadCount = 0; // goes up on every scene load (Unity 6 SceneHandle is no longer an int)
    private bool suppressAutoSave = false;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadGame();
    }

    void OnDestroy()
    {
        if (instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        sceneLoadCount++;
        suppressAutoSave = false;
        BuildItemRegistry();
    }

    void OnApplicationQuit()
    {
        // Only autosave while actually inside the game level (not in the menu, not after permadeath/ending)
        if (!suppressAutoSave && RespawnManager.instance != null)
        {
            if (!TrySavePlayerCheckpoint()) WriteToFile();
        }
    }

    // ------------------------------------------------------------------ checkpoint autosave
    // RESUME = EXACTLY WHERE THE PLAYER STOPPED:
    //  - every 0.25 s the player's position is remembered (only while he stands normally on the ground: not during a
    //    cinematic / the drag / waking up / hiding / riding the elevator / mid-jump),
    //  - that position is written to the save file every 5 s, when the game is closed, and on every manual save.
    // Any teleport that saves its own spot (basement drop, respawn point) clears the remembered position, so an older
    // spot can never overwrite it.
    private const float checkpointInterval = 5f;
    private float nextCheckpointTime = 0f;
    private float nextSampleTime = 0f;
    private PlayerController cachedPlayer;

    private Vector3 lastGoodPos;
    private Quaternion lastGoodRot;
    private bool hasLastGood;
    private int lastGoodSceneLoad = -1;

    void Update()
    {
        if (Time.unscaledTime >= nextSampleTime)
        {
            nextSampleTime = Time.unscaledTime + 0.25f;
            SampleGoodPosition(false);
        }
        if (Time.unscaledTime < nextCheckpointTime) return;
        nextCheckpointTime = Time.unscaledTime + checkpointInterval;
        TrySavePlayerCheckpoint();
    }

    /// <summary>Remembers where the player stands, if it is a normal spot to resume from.</summary>
    private void SampleGoodPosition(bool manual)
    {
        if (suppressAutoSave || RespawnManager.instance == null) return;      // not in the game level
        if (restoredSceneLoad != sceneLoadCount) return;                      // world not restored yet

        if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerController>();
        PlayerController pc = cachedPlayer;
        if (pc == null || (!pc.enabled && !manual) || pc.isDead || pc.isHidden || pc.isCinematicPlaying) return;
        if (pc.transform.parent != null) return;                              // riding the elevator

        PlayerStats stats = pc.GetComponent<PlayerStats>();
        if (stats != null && stats.IsDead()) return;

        CharacterController cc = pc.GetComponent<CharacterController>();
        if (cc != null && (!cc.enabled || (!cc.isGrounded && !manual))) return; // drag / wake-up / mid-jump

        lastGoodPos = pc.transform.position;
        lastGoodRot = pc.transform.rotation;
        hasLastGood = true;
        lastGoodSceneLoad = sceneLoadCount;
    }

    /// <summary>Manual save from the pause menu (FR_38). Same safety rules as the automatic checkpoint.</summary>
    public bool TryManualCheckpoint()
    {
        return TrySavePlayerCheckpoint(true);
    }

    /// <summary>True while the player is inside the game level (not in the main menu).</summary>
    public bool IsInGameLevel => RespawnManager.instance != null && !suppressAutoSave;

    // ------------------------------------------------------------------ progression events (objectives, achievements, stats)
    public bool HasEvent(string id) => currentSaveData != null && ListContains(currentSaveData.eventsCSV, Sanitize(id));

    /// <summary>Stores a one-time progression event. Returns true only the first time.</summary>
    public bool RecordEvent(string id, bool countsAsProgress)
    {
        id = Sanitize(id);
        if (string.IsNullOrEmpty(id) || HasEvent(id)) return false;
        currentSaveData.eventsCSV = ListAdd(currentSaveData.eventsCSV, id);
        if (countsAsProgress) PlayerBehaviorTracker.ReportProgress();
        WriteToFile();
        return true;
    }

    public bool HasReadNote(string noteId) => currentSaveData != null && ListContains(currentSaveData.notesReadCSV, Sanitize(noteId));

    public bool RecordNoteRead(string noteId)
    {
        noteId = Sanitize(noteId);
        if (string.IsNullOrEmpty(noteId) || HasReadNote(noteId)) return false;
        currentSaveData.notesReadCSV = ListAdd(currentSaveData.notesReadCSV, noteId);
        WriteToFile();
        return true;
    }

    public bool RecordItemFound(string displayName)
    {
        displayName = Sanitize(displayName);
        if (string.IsNullOrEmpty(displayName) || ListContains(currentSaveData.itemsFoundCSV, displayName)) return false;
        currentSaveData.itemsFoundCSV = ListAdd(currentSaveData.itemsFoundCSV, displayName);
        WriteToFile();
        return true;
    }

    public bool RecordLoreFound(string loreId)
    {
        loreId = Sanitize(loreId);
        if (string.IsNullOrEmpty(loreId) || ListContains(currentSaveData.loreFoundCSV, loreId)) return false;
        currentSaveData.loreFoundCSV = ListAdd(currentSaveData.loreFoundCSV, loreId);
        WriteToFile();
        return true;
    }

    public static List<string> SplitList(string csv)
    {
        List<string> result = new List<string>();
        if (string.IsNullOrEmpty(csv)) return result;
        foreach (string s in csv.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)) result.Add(s);
        return result;
    }

    public static int CountList(string csv) => SplitList(csv).Count;

    /// <summary>Writes the current state to disk right now (used by the pause menu before leaving the level).</summary>
    public void SaveNow()
    {
        if (!TrySavePlayerCheckpoint()) WriteToFile();
    }

    /// <summary>Writes the player's last normal position (see above) to the save file.</summary>
    private bool TrySavePlayerCheckpoint() => TrySavePlayerCheckpoint(false);

    /// <param name="manual">True when the pause menu asks (the PlayerController is disabled while the game is paused).</param>
    private bool TrySavePlayerCheckpoint(bool manual)
    {
        if (suppressAutoSave || RespawnManager.instance == null) return false;      // not in the game level
        if (restoredSceneLoad != sceneLoadCount) return false;                      // world not restored yet

        SampleGoodPosition(manual);                                                 // take the very latest spot if possible
        if (!hasLastGood || lastGoodSceneLoad != sceneLoadCount) return false;

        // FR_40: also remember where the enemy is, so Resume / Load puts him back (only while he is calmly patrolling/searching)
        currentSaveData.hasEnemyPosition = false;
        foreach (EnemyAI enemy in FindObjectsByType<EnemyAI>())
        {
            EnemyAI.AIState s = enemy.State;
            if (enemy.isActiveAndEnabled && (s == EnemyAI.AIState.Patrol || s == EnemyAI.AIState.Search || s == EnemyAI.AIState.InvestigateSound))
            {
                Vector3 e = enemy.transform.position;
                currentSaveData.enemyPosition = new float[] { e.x, e.y, e.z };
                currentSaveData.hasEnemyPosition = true;
                break;
            }
        }

        // RespawnManager adds +1m on load (for the floor-level basement point) - compensate so we come back where we stood
        Vector3 pos = lastGoodPos - Vector3.up * 0.9f;
        Quaternion rot = lastGoodRot;
        SavePlayerTransform(pos, rot);
        // SavePlayerTransform clears the remembered spot; keep it, it is the one we just wrote
        lastGoodPos = pos + Vector3.up * 0.9f; lastGoodRot = rot; hasLastGood = true; lastGoodSceneLoad = sceneLoadCount;
        return true;
    }

    // ------------------------------------------------------------------ CSV helpers
    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace(",", "_").Replace(";", "_").Replace("|", "_");
    }

    private static bool ListContains(string csv, string value)
    {
        if (string.IsNullOrEmpty(csv) || string.IsNullOrEmpty(value)) return false;
        foreach (string entry in csv.Split(','))
        {
            if (entry == value) return true;
        }
        return false;
    }

    private static string ListAdd(string csv, string value)
    {
        if (string.IsNullOrEmpty(value) || ListContains(csv, value)) return csv;
        return csv + value + ",";
    }

    private static string ListRemove(string csv, string value)
    {
        if (string.IsNullOrEmpty(csv)) return "";
        List<string> parts = new List<string>(csv.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        parts.RemoveAll(p => p == value);
        return parts.Count == 0 ? "" : string.Join(",", parts) + ",";
    }

    /// <summary>Stable ID for a scene object, based on its name and position (same format doors always used).</summary>
    public static string GetSceneObjectId(Transform t)
    {
        int x = Mathf.RoundToInt(t.position.x * 10f);
        int y = Mathf.RoundToInt(t.position.y * 10f);
        int z = Mathf.RoundToInt(t.position.z * 10f);
        return Sanitize(t.name) + "_" + x + "_" + y + "_" + z;
    }

    // ------------------------------------------------------------------ doors
    public bool IsDoorUnlocked(string doorId) => ListContains(currentSaveData.unlockedDoorsCSV, Sanitize(doorId));

    public void UnlockDoor(string doorId)
    {
        doorId = Sanitize(doorId);
        if (IsDoorUnlocked(doorId)) return;
        currentSaveData.unlockedDoorsCSV = ListAdd(currentSaveData.unlockedDoorsCSV, doorId);
        PlayerBehaviorTracker.ReportProgress(); // a door opened = a puzzle/section step was completed
        WriteToFile();
    }

    // ------------------------------------------------------------------ flags (one-time events)
    public bool HasFlag(string flag) => ListContains(currentSaveData.flagsCSV, Sanitize(flag));

    public void SetFlag(string flag)
    {
        flag = Sanitize(flag);
        if (HasFlag(flag)) return;
        currentSaveData.flagsCSV = ListAdd(currentSaveData.flagsCSV, flag);
        PlayerBehaviorTracker.ReportProgress();
        WriteToFile();
    }

    public void ClearFlag(string flag)
    {
        flag = Sanitize(flag);
        if (!HasFlag(flag)) return;
        currentSaveData.flagsCSV = ListRemove(currentSaveData.flagsCSV, flag);
        WriteToFile();
    }

    // ------------------------------------------------------------------ item registry
    private void BuildItemRegistry()
    {
        itemIds.Clear();
        itemsById.Clear();
        registrySceneLoad = sceneLoadCount;

        List<GameObject> items = new List<GameObject>();
        foreach (KeyItem k in FindObjectsByType<KeyItem>(FindObjectsInactive.Include)) items.Add(k.gameObject);
        foreach (ThrowableItem t in FindObjectsByType<ThrowableItem>(FindObjectsInactive.Include)) items.Add(t.gameObject);
        foreach (SmashableJar j in FindObjectsByType<SmashableJar>(FindObjectsInactive.Include))
        {
            items.Add(j.gameObject);
            if (j.itemInside != null) items.Add(j.itemInside);
        }

        // Sort so duplicate IDs always get the same suffix, every run
        items.Sort((a, b) => string.CompareOrdinal(a.name + a.transform.GetSiblingIndex(), b.name + b.transform.GetSiblingIndex()));

        foreach (GameObject go in items)
        {
            if (go == null || itemIds.ContainsKey(go)) continue;
            string id = GetSceneObjectId(go.transform);
            string unique = id;
            int n = 2;
            while (itemsById.ContainsKey(unique)) unique = id + "#" + n++;
            itemIds[go] = unique;
            itemsById[unique] = go;
        }
    }

    private void EnsureRegistry()
    {
        if (registrySceneLoad != sceneLoadCount) BuildItemRegistry();
    }

    public string GetItemId(GameObject item)
    {
        if (item == null) return "";
        EnsureRegistry();
        if (itemIds.TryGetValue(item, out string id)) return id;
        return Sanitize(item.name);
    }

    /// <summary>Finds an item by its saved ID (also finds inactive objects). Old saves that stored plain names still work.</summary>
    public GameObject FindItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureRegistry();
        if (itemsById.TryGetValue(id, out GameObject go) && go != null) return go;

        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t.gameObject.scene.IsValid() && Sanitize(t.name) == id) return t.gameObject;
        }
        return null;
    }

    // ------------------------------------------------------------------ item world state
    public bool IsItemCollected(string itemId) => ListContains(currentSaveData.collectedItemsCSV, itemId);

    public bool IsItemDropped(string itemId)
    {
        foreach (var entry in GetDroppedItems())
        {
            if (entry.Key == itemId) return true;
        }
        return false;
    }

    /// <summary>False when the item was picked up earlier and is neither lying on the floor nor in the player's hand.</summary>
    public bool ShouldItemExistInWorld(string itemId)
    {
        return !IsItemCollected(itemId) || IsItemDropped(itemId) || currentSaveData.heldItemName == itemId;
    }

    public List<KeyValuePair<string, Vector3>> GetDroppedItems()
    {
        List<KeyValuePair<string, Vector3>> list = new List<KeyValuePair<string, Vector3>>();
        if (string.IsNullOrEmpty(currentSaveData.droppedItemsCSV)) return list;

        foreach (string entry in currentSaveData.droppedItemsCSV.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string[] p = entry.Split('|');
            if (p.Length < 4) continue;
            float x, y, z;
            if (float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) &&
                float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y) &&
                float.TryParse(p[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z))
            {
                list.Add(new KeyValuePair<string, Vector3>(p[0], new Vector3(x, y, z)));
            }
        }
        return list;
    }

    private void SetDroppedItems(List<KeyValuePair<string, Vector3>> list)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var e in list)
        {
            sb.Append(e.Key).Append('|')
              .Append(e.Value.x.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('|')
              .Append(e.Value.y.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('|')
              .Append(e.Value.z.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(';');
        }
        currentSaveData.droppedItemsCSV = sb.ToString();
    }

    private void SetDroppedInternal(string itemId, Vector3 pos, bool remove)
    {
        List<KeyValuePair<string, Vector3>> list = GetDroppedItems();
        list.RemoveAll(e => e.Key == itemId);
        if (!remove) list.Add(new KeyValuePair<string, Vector3>(itemId, pos));
        SetDroppedItems(list);
    }

    /// <summary>Remembers where an item was FIRST found (only the first time).</summary>
    public void RecordItemHome(GameObject item)
    {
        if (item == null || currentSaveData == null) return;
        string id = GetItemId(item);
        if (string.IsNullOrEmpty(id) || TryGetItemHome(id, out _)) return;
        Vector3 p = item.transform.position;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        currentSaveData.itemHomesCSV = (currentSaveData.itemHomesCSV ?? "") + id + "|" + p.x.ToString(ci) + "|" + p.y.ToString(ci) + "|" + p.z.ToString(ci) + ";";
    }

    public bool TryGetItemHome(string id, out Vector3 pos)
    {
        pos = Vector3.zero;
        if (currentSaveData == null || string.IsNullOrEmpty(currentSaveData.itemHomesCSV)) return false;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        foreach (string e in currentSaveData.itemHomesCSV.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string[] p = e.Split('|');
            if (p.Length < 4 || p[0] != id) continue;
            if (float.TryParse(p[1], System.Globalization.NumberStyles.Float, ci, out float x) &&
                float.TryParse(p[2], System.Globalization.NumberStyles.Float, ci, out float y) &&
                float.TryParse(p[3], System.Globalization.NumberStyles.Float, ci, out float z)) { pos = new Vector3(x, y, z); return true; }
        }
        return false;
    }

    public void OnItemPickedUp(GameObject item)
    {
        string id = GetItemId(item);
        currentSaveData.collectedItemsCSV = ListAdd(currentSaveData.collectedItemsCSV, id);
        SetDroppedInternal(id, Vector3.zero, true);
        currentSaveData.heldItemName = id;
        WriteToFile();
    }

    public void OnItemDropped(GameObject item)
    {
        string id = GetItemId(item);
        SetDroppedInternal(id, item.transform.position, false);
        if (currentSaveData.heldItemName == id) currentSaveData.heldItemName = "";
        WriteToFile();
    }

    /// <summary>Item was used up (key in a door, valve on a pipe...).</summary>
    /// <param name="respawnOnReload">True for puzzle items: puzzles reset on death, so the item must come back too (no softlock).</param>
    public void OnItemConsumed(GameObject item, bool respawnOnReload)
    {
        string id = GetItemId(item);
        SetDroppedInternal(id, Vector3.zero, true);
        if (currentSaveData.heldItemName == id) currentSaveData.heldItemName = "";
        if (respawnOnReload) currentSaveData.collectedItemsCSV = ListRemove(currentSaveData.collectedItemsCSV, id);
        WriteToFile();
    }

    /// <summary>An item was released into the world by something other than the player (e.g. it fell out of a smashed jar).</summary>
    public void OnItemReleased(GameObject item)
    {
        SetDroppedInternal(GetItemId(item), item.transform.position, false);
        WriteToFile();
    }

    /// <summary>An item is gone for good (e.g. jar smashed) - it stays collected but no longer lies anywhere.</summary>
    public void OnItemDestroyed(GameObject item)
    {
        string id = GetItemId(item);
        currentSaveData.collectedItemsCSV = ListAdd(currentSaveData.collectedItemsCSV, id);
        SetDroppedInternal(id, Vector3.zero, true);
        WriteToFile();
    }

    public void MarkWorldRestored()
    {
        restoredSceneLoad = sceneLoadCount;
    }

    private void RefreshDroppedPositions()
    {
        if (restoredSceneLoad != sceneLoadCount) return;

        List<KeyValuePair<string, Vector3>> list = GetDroppedItems();
        for (int i = list.Count - 1; i >= 0; i--)
        {
            GameObject go = FindItem(list[i].Key);
            if (go == null) { list.RemoveAt(i); continue; } // destroyed in the world
            list[i] = new KeyValuePair<string, Vector3>(list[i].Key, go.transform.position);
        }
        SetDroppedItems(list);
    }

    // ------------------------------------------------------------------ player
    public void SavePlayerTransform(Vector3 position, Quaternion rotation)
    {
        hasLastGood = false;   // a teleport saved its own spot (basement drop, respawn point): never overwrite it with an older one
        currentSaveData.playerPosition = new float[] { position.x, position.y, position.z };
        currentSaveData.playerRotation = new float[] { rotation.x, rotation.y, rotation.z, rotation.w };
        WriteToFile();
    }

    public bool TryGetPlayerRotation(out Quaternion rotation)
    {
        float[] r = currentSaveData.playerRotation;
        rotation = Quaternion.identity;
        if (r == null || r.Length < 4) return false;
        if (Mathf.Approximately(r[0], 0f) && Mathf.Approximately(r[1], 0f) && Mathf.Approximately(r[2], 0f) && Mathf.Approximately(r[3], 0f)) return false;
        rotation = new Quaternion(r[0], r[1], r[2], r[3]);
        return true;
    }

    public void DecreaseLife()
    {
        currentSaveData.lives = Mathf.Max(0, currentSaveData.lives - 1);
        // Save immediately when losing a life so they can't cheat by Alt+F4
        WriteToFile();
    }

    public void SaveWorldState() => WriteToFile();

    // ------------------------------------------------------------------ file IO
    private void WriteToFile()
    {
        RefreshDroppedPositions();
        currentSaveData.savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        WriteSaveFileSafe(SaveFilePath, currentSaveData);
    }

    // ------------------------------------------------------------------ safe file IO (FR_41)
    public static string BackupPathFor(string path) => path + ".bak";

    /// <summary>
    /// Corruption-safe write: the JSON is written to a temp file first, the old save is kept as a .bak backup,
    /// and only then is the temp file moved into place. A crash mid-write can never leave a half-written save.
    /// </summary>
    public static bool WriteSaveFileSafe(string path, SaveData data)
    {
        if (data == null) return false;
        string tmp = path + ".tmp";
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(tmp, json);
            if (File.Exists(path))
            {
                File.Copy(path, BackupPathFor(path), true);
                File.Delete(path);
            }
            File.Move(tmp, path);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("SaveManager: Could not write save file: " + e.Message);
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            return false;
        }
    }

    /// <summary>Reads and VALIDATES a save file. Returns false for a missing, empty, unreadable or nonsensical file.</summary>
    public static bool TryReadSaveFile(string path, out SaveData data)
    {
        data = null;
        try
        {
            if (!File.Exists(path)) return false;
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{")) return false;
            data = JsonUtility.FromJson<SaveData>(json);
        }
        catch (System.Exception)
        {
            data = null;
            return false;
        }
        if (data == null || !IsValid(data)) { data = null; return false; }
        Repair(data);
        return true;
    }

    private static bool IsValid(SaveData d)
    {
        if (d.lives < 0 || d.lives > 3) return false;
        if (float.IsNaN(d.playTime) || d.playTime < 0f) return false;
        if (d.playerPosition != null)
        {
            foreach (float f in d.playerPosition)
                if (float.IsNaN(f) || float.IsInfinity(f) || Mathf.Abs(f) > 100000f) return false;
        }
        if (d.aggressionLevel < 0 || d.aggressionLevel > 5) return false;
        return true;
    }

    private static void Repair(SaveData d)
    {
        if (d.playerPosition == null || d.playerPosition.Length < 3) d.playerPosition = new float[3];
        if (d.playerRotation == null || d.playerRotation.Length < 4) d.playerRotation = new float[4];
        if (d.heldItemName == null) d.heldItemName = "";
        if (d.itemHomesCSV == null) d.itemHomesCSV = "";
        if (d.droppedItemsCSV == null) d.droppedItemsCSV = "";
        if (d.unlockedDoorsCSV == null) d.unlockedDoorsCSV = "";
        if (d.collectedItemsCSV == null) d.collectedItemsCSV = "";
        if (d.flagsCSV == null) d.flagsCSV = "";
        if (d.skillClass == null) d.skillClass = "Balanced";
        if (d.hidingSpotsCSV == null) d.hidingSpotsCSV = "";
        if (d.eventsCSV == null) d.eventsCSV = "";
        if (d.notesReadCSV == null) d.notesReadCSV = "";
        if (d.itemsFoundCSV == null) d.itemsFoundCSV = "";
        if (d.savedAt == null) d.savedAt = "";
        if (d.loreFoundCSV == null) d.loreFoundCSV = "";
        if (d.enemyPosition == null || d.enemyPosition.Length < 3) { d.enemyPosition = new float[3]; d.hasEnemyPosition = false; }
    }

    public void LoadGame()
    {
        // BUG FIX: forget the old position, so a checkpoint in the same frame cannot write it into the save just loaded
        hasLastGood = false;
        restoredSceneLoad = -1;
        currentSaveData = null;
        if (!TryReadSaveFile(SaveFilePath, out currentSaveData))
        {
            if (File.Exists(SaveFilePath))
                Debug.LogWarning("SaveManager: Save file failed validation - trying the backup copy.");

            // Fall back to the last good backup
            if (TryReadSaveFile(BackupPathFor(SaveFilePath), out currentSaveData))
                Debug.Log("SaveManager: Restored the game from the backup save.");
            else if (File.Exists(SaveFilePath))
                Debug.LogWarning("SaveManager: Save file was corrupted and no valid backup exists, starting fresh.");
        }

        if (currentSaveData == null) currentSaveData = new SaveData();
        Repair(currentSaveData);
    }

    public void DeleteSave()
    {
        try
        {
            if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
            if (File.Exists(BackupPathFor(SaveFilePath))) File.Delete(BackupPathFor(SaveFilePath));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("SaveManager: Could not delete save file: " + e.Message);
        }
        currentSaveData = new SaveData();
        suppressAutoSave = true; // don't recreate the file on quit (permadeath / game finished)
    }

    public Vector3 GetVector3(float[] array)
    {
        if (array == null || array.Length < 3) return Vector3.zero;
        return new Vector3(array[0], array[1], array[2]);
    }
}
