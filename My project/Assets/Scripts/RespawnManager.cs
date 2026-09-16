using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class RespawnManager : MonoBehaviour
{
    public static RespawnManager instance;

    [Header("Respawn Points")]
    public Transform basementRespawn;
    public Transform dayRoomRespawn;

    [Header("Cinematic UI")]
    public CanvasGroup blackScreen;
    public TextMeshProUGUI spookyText;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        // 1. Setup UI
        if (blackScreen != null)
        {
            blackScreen.alpha = 1f; // Start black if loading in
            StartCoroutine(FadeOutBlackScreen());
        }

        // 2. Load Save Data and Position Player & Items
        if (SaveManager.instance != null)
        {
            ApplySaveData();
        }
    }

    private void ApplySaveData()
    {
        SaveData data = SaveManager.instance.currentSaveData;

        // Place Player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && data.playerPosition != null && data.playerPosition.Length >= 3)
        {
            Vector3 savedPos = SaveManager.instance.GetVector3(data.playerPosition);
            if (savedPos != Vector3.zero)
            {
                // Disable CharacterController temporarily to teleport
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                
                player.transform.position = savedPos;
                
                if (cc != null) cc.enabled = true;
                Debug.Log("Player respawned at saved position.");
            }
        }

        // Place Dropped Item
        if (!string.IsNullOrEmpty(data.droppedItemName))
        {
            GameObject droppedItem = GameObject.Find(data.droppedItemName);
            if (droppedItem != null)
            {
                Vector3 dropPos = SaveManager.instance.GetVector3(data.droppedItemPosition);
                droppedItem.transform.position = dropPos;
                
                // Enable colliders so it can be picked up
                Collider[] cols = droppedItem.GetComponentsInChildren<Collider>();
                foreach (Collider c in cols) c.enabled = true;

                Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }
                Debug.Log("Dropped item placed at death location: " + data.droppedItemName);
            }
        }
    }

    private IEnumerator FadeOutBlackScreen()
    {
        yield return new WaitForSeconds(2f); // Show text for a bit
        
        if (spookyText != null) spookyText.text = ""; // clear text
        
        while (blackScreen != null && blackScreen.alpha > 0)
        {
            blackScreen.alpha -= Time.deltaTime * 0.5f;
            yield return null;
        }
    }

    public void TriggerCinematicDeath(int livesRemaining, GameObject player)
    {
        StartCoroutine(DeathRoutine(livesRemaining, player));
    }

    private IEnumerator DeathRoutine(int livesRemaining, GameObject player)
    {
        // Fade to black
        if (blackScreen != null)
        {
            while (blackScreen.alpha < 1f)
            {
                blackScreen.alpha += Time.deltaTime;
                yield return null;
            }
        }

        // Show Spooky Text
        if (spookyText != null)
        {
            if (livesRemaining == 2)
                spookyText.text = "Did you really think you could hide?";
            else if (livesRemaining == 1)
                spookyText.text = "He is getting closer... You are running out of time.";
        }

        yield return new WaitForSeconds(3f); // Read time

        // Decide Respawn Point (Random between basement and dayroom for now)
        Transform chosenRespawn = (Random.value > 0.5f && basementRespawn != null) ? basementRespawn : dayRoomRespawn;
        
        // Save the new respawn position to the SaveManager
        if (chosenRespawn != null && SaveManager.instance != null)
        {
            SaveManager.instance.currentSaveData.playerPosition[0] = chosenRespawn.position.x;
            SaveManager.instance.currentSaveData.playerPosition[1] = chosenRespawn.position.y;
            SaveManager.instance.currentSaveData.playerPosition[2] = chosenRespawn.position.z;
            
            // Save immediately
            string json = JsonUtility.ToJson(SaveManager.instance.currentSaveData, true);
            System.IO.File.WriteAllText(Application.persistentDataPath + "/AsylumSave.json", json);
        }

        // Reload Scene (It will auto-place the player and dropped item in Start)
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
