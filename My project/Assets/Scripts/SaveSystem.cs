using System.IO;
using UnityEngine;

public class SaveSystem : MonoBehaviour
{
    [System.Serializable]
    public class SaveData
    {
        public float health;
        public float stamina;
        public float[] playerPosition = new float[3];
        public System.Collections.Generic.List<string> inventoryItems;
    }

    private string saveFilePath;

    void Start()
    {
        saveFilePath = Path.Combine(Application.persistentDataPath, "savegame.json");
    }

    public void SaveGame()
    {
        PlayerStats playerStats = FindAnyObjectByType<PlayerStats>();
        Inventory inventory = FindAnyObjectByType<Inventory>();

        if (playerStats == null || inventory == null)
        {
            Debug.LogError("Cannot save: PlayerStats or Inventory components not found in scene!");
            return;
        }

        SaveData data = new SaveData();
        data.health = playerStats.GetHealth();
        data.stamina = playerStats.GetStamina();
        
        Vector3 pos = playerStats.transform.position;
        data.playerPosition[0] = pos.x;
        data.playerPosition[1] = pos.y;
        data.playerPosition[2] = pos.z;

        data.inventoryItems = inventory.GetAllItems();

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(saveFilePath, json);
        Debug.Log("Game Saved Successfully to: " + saveFilePath);
    }

    public void LoadGame()
    {
        if (!File.Exists(saveFilePath))
        {
            Debug.LogWarning("No save file found at: " + saveFilePath);
            return;
        }

        PlayerStats playerStats = FindAnyObjectByType<PlayerStats>();
        Inventory inventory = FindAnyObjectByType<Inventory>();

        if (playerStats == null || inventory == null)
        {
            Debug.LogError("Cannot load: PlayerStats or Inventory components not found in scene!");
            return;
        }

        string json = File.ReadAllText(saveFilePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        CharacterController controller = playerStats.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        playerStats.transform.position = new Vector3(data.playerPosition[0], data.playerPosition[1], data.playerPosition[2]);

        if (controller != null) controller.enabled = true;

        playerStats.LoadStats(data.health, data.stamina);
        inventory.LoadInventory(data.inventoryItems);

        Debug.Log("Game Loaded Successfully from: " + saveFilePath);
    }

    public void DeleteSave()
    {
        if (File.Exists(saveFilePath))
        {
            File.Delete(saveFilePath);
            Debug.Log("Save file deleted.");
        }
    }
}
