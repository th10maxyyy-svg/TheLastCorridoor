using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MANUAL SAVE SLOTS (FR_38, FR_42).
/// The game keeps ONE live save file (AsylumSave.json) that the checkpoint system writes to all the time.
/// A manual save copies that live state into one of three slot files. Loading a slot copies it back and reloads the level,
/// so every existing system (world state, items, doors, adaptive AI) restores itself exactly as on a normal resume.
/// </summary>
public static class SaveSlots
{
    public const int SlotCount = 3;
    public const string GameSceneName = "neww";

    public struct SlotInfo
    {
        public int slot;          // 0 = autosave (live file), 1..3 = manual slots
        public bool exists;
        public bool valid;
        public string label;
        public string details;
    }

    public static string SlotPath(int slot)
    {
        if (slot <= 0) return SaveManager.SaveFilePath;
        return Path.Combine(Application.persistentDataPath, "AsylumSave_Slot" + slot + ".json");
    }

    public static SlotInfo GetInfo(int slot)
    {
        SlotInfo info = new SlotInfo { slot = slot, label = slot == 0 ? "Autosave (last checkpoint)" : "Slot " + slot };
        string path = SlotPath(slot);
        info.exists = File.Exists(path);
        if (!info.exists)
        {
            info.details = "Empty";
            return info;
        }

        if (SaveManager.TryReadSaveFile(path, out SaveData data))
        {
            info.valid = true;
            info.details = Describe(data);
        }
        else if (SaveManager.TryReadSaveFile(SaveManager.BackupPathFor(path), out data))
        {
            info.valid = true;
            info.details = Describe(data) + "  (restored from backup)";
        }
        else
        {
            info.valid = false;
            info.details = "Corrupted - cannot be loaded";
        }
        return info;
    }

    private static string Describe(SaveData d)
    {
        string when = string.IsNullOrEmpty(d.savedAt) ? "" : d.savedAt + "   ";
        int puzzles = GameProgress.CountSolvedObjectives(d);
        return when + "Played " + FormatTime(d.playTime) + "   Lives " + d.lives + "/3   Objectives " + puzzles;
    }

    public static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int h = s / 3600, m = (s % 3600) / 60, sec = s % 60;
        return h > 0 ? string.Format("{0}:{1:00}:{2:00}", h, m, sec) : string.Format("{0}:{1:00}", m, sec);
    }

    /// <summary>Saves the current game into a manual slot. Returns a message for the player.</summary>
    public static bool SaveToSlot(int slot, out string message)
    {
        message = "";
        SaveManager sm = SaveManager.instance;
        if (sm == null || !sm.IsInGameLevel)
        {
            message = "You can only save inside the asylum.";
            return false;
        }

        if (!sm.TryManualCheckpoint())
        {
            message = "You can't save right now - he is hunting you, or you are hidden / in the air.";
            return false;
        }

        if (!SaveManager.WriteSaveFileSafe(SlotPath(slot), sm.currentSaveData))
        {
            message = "Saving failed - check the disk.";
            return false;
        }

        message = "Game saved to Slot " + slot + ".";
        return true;
    }

    /// <summary>Loads a slot (0 = autosave) and restarts the level from it.</summary>
    public static bool LoadSlot(int slot, out string message)
    {
        message = "";
        string path = SlotPath(slot);

        SaveData data;
        if (!SaveManager.TryReadSaveFile(path, out data) && !SaveManager.TryReadSaveFile(SaveManager.BackupPathFor(path), out data))
        {
            message = "That save is empty or corrupted.";
            return false;
        }

        if (slot != 0)
        {
            if (!SaveManager.WriteSaveFileSafe(SaveManager.SaveFilePath, data))
            {
                message = "Could not load the save.";
                return false;
            }
        }

        IntroState.skipIntro = true;
        EnemyRestore.RestoreOnNextLoad = true; // FR_40: put the enemy back where he was
        if (SaveManager.instance != null) SaveManager.instance.LoadGame();

        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneLoader.Load(GameSceneName);
        message = "Loading...";
        return true;
    }

    public static void DeleteSlot(int slot)
    {
        if (slot <= 0) return;
        try
        {
            string path = SlotPath(slot);
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(SaveManager.BackupPathFor(path))) File.Delete(SaveManager.BackupPathFor(path));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("SaveSlots: could not delete slot " + slot + ": " + e.Message);
        }
    }
}
