using UnityEngine;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public int lives = 3;
    public float[] playerPosition = new float[3];
    public float[] playerRotation = new float[4];
    public string heldItemName = "";
    
    // To save the location of the dropped item if the player dies and drops it
    public string droppedItemName = "";
    public float[] droppedItemPosition = new float[3];
}

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;
    private string saveFilePath;
    
    public SaveData currentSaveData = new SaveData();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        saveFilePath = Application.persistentDataPath + "/AsylumSave.json";
        LoadGame();
    }

    public void SaveGame(Transform playerTransform, string heldItem, string droppedItem = "", Vector3 droppedPos = default)
    {
        // 1. Save Player Position & Rotation
        if (playerTransform != null)
        {
            currentSaveData.playerPosition[0] = playerTransform.position.x;
            currentSaveData.playerPosition[1] = playerTransform.position.y;
            currentSaveData.playerPosition[2] = playerTransform.position.z;

            currentSaveData.playerRotation[0] = playerTransform.rotation.x;
            currentSaveData.playerRotation[1] = playerTransform.rotation.y;
            currentSaveData.playerRotation[2] = playerTransform.rotation.z;
            currentSaveData.playerRotation[3] = playerTransform.rotation.w;
        }

        // 2. Save Item Data
        currentSaveData.heldItemName = heldItem != null ? heldItem : "";
        currentSaveData.droppedItemName = droppedItem;
        
        if (!string.IsNullOrEmpty(droppedItem))
        {
            currentSaveData.droppedItemPosition[0] = droppedPos.x;
            currentSaveData.droppedItemPosition[1] = droppedPos.y;
            currentSaveData.droppedItemPosition[2] = droppedPos.z;
        }

        // 3. Write to File
        string json = JsonUtility.ToJson(currentSaveData, true);
        File.WriteAllText(saveFilePath, json);
        Debug.Log("Game Saved to: " + saveFilePath);
    }

    public void LoadGame()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            currentSaveData = JsonUtility.FromJson<SaveData>(json);
            Debug.Log("Game Loaded successfully.");
        }
        else
        {
            Debug.Log("No save file found. Starting fresh.");
            currentSaveData = new SaveData(); // Reset
        }
    }

    public void DeleteSave()
    {
        if (File.Exists(saveFilePath))
        {
            File.Delete(saveFilePath);
            Debug.Log("Save file wiped! (Permadeath)");
        }
        currentSaveData = new SaveData();
    }
    
    public void DecreaseLife()
    {
        currentSaveData.lives--;
        // Save immediately when losing a life so they can't cheat by Alt+F4
        SaveGame(null, currentSaveData.heldItemName, currentSaveData.droppedItemName, GetVector3(currentSaveData.droppedItemPosition));
    }
    
    public Vector3 GetVector3(float[] array)
    {
        if (array == null || array.Length < 3) return Vector3.zero;
        return new Vector3(array[0], array[1], array[2]);
    }
}
