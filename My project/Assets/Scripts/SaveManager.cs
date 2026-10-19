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

    // Every item lying on the floor away from its original spot: "id|x|y|z;id|x|y|z;"
    public string droppedItemsCSV = "";

    // WORLD STATE PERSISTENCE (CSV strings because JsonUtility can't save dictionaries)
    public string unlockedDoorsCSV = "";
    public string collectedItemsCSV = "";
    public string flagsCSV = ""; // One-time events (headlamp picked, trap disabled, ambush played...)
    public bool isNightmareActive = false;
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
        if (!suppressAutoSave && RespawnManager.instance != null) WriteToFile();
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
        WriteToFile();
    }

    // ------------------------------------------------------------------ flags (one-time events)
    public bool HasFlag(string flag) => ListContains(currentSaveData.flagsCSV, Sanitize(flag));

    public void SetFlag(string flag)
    {
        flag = Sanitize(flag);
        if (HasFlag(flag)) return;
        currentSaveData.flagsCSV = ListAdd(currentSaveData.flagsCSV, flag);
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
        try
        {
            string json = JsonUtility.ToJson(currentSaveData, true);
            File.WriteAllText(SaveFilePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError("SaveManager: Could not write save file: " + e.Message);
        }
    }

    public void LoadGame()
    {
        currentSaveData = null;
        try
        {
            if (File.Exists(SaveFilePath))
            {
                currentSaveData = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveFilePath));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("SaveManager: Save file was corrupted, starting fresh. (" + e.Message + ")");
            currentSaveData = null;
        }

        if (currentSaveData == null) currentSaveData = new SaveData();
        if (currentSaveData.playerPosition == null || currentSaveData.playerPosition.Length < 3) currentSaveData.playerPosition = new float[3];
        if (currentSaveData.playerRotation == null || currentSaveData.playerRotation.Length < 4) currentSaveData.playerRotation = new float[4];
        if (currentSaveData.heldItemName == null) currentSaveData.heldItemName = "";
        if (currentSaveData.droppedItemsCSV == null) currentSaveData.droppedItemsCSV = "";
        if (currentSaveData.unlockedDoorsCSV == null) currentSaveData.unlockedDoorsCSV = "";
        if (currentSaveData.collectedItemsCSV == null) currentSaveData.collectedItemsCSV = "";
        if (currentSaveData.flagsCSV == null) currentSaveData.flagsCSV = "";
    }

    public void DeleteSave()
    {
        try
        {
            if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
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
