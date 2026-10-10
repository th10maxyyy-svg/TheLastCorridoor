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
    public UnityEngine.UI.Image deathImage; // Image to show instead of text

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        // 1. Setup UI
        if (spookyText != null) spookyText.text = ""; // Editor wala text foran clear kar do!

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

        // After a death-respawn reload make sure the player is never left frozen
        if (IntroState.skipIntro) StartCoroutine(EnsurePlayerFree());
    }

    private IEnumerator EnsurePlayerFree()
    {
        // Let every Start() (IntroCinematicManager, PlayerController, ...) finish first
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.2f);

        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            pc.ForceUnlock();
            Debug.Log("[Respawn] Player unlocked after respawn.");
        }

        // Projector video must never play after a respawn
        foreach (UnityEngine.Video.VideoPlayer vp in FindObjectsByType<UnityEngine.Video.VideoPlayer>(FindObjectsInactive.Include))
        {
            vp.Stop();
            vp.enabled = false;
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
                // Zameen se thora sa oopar rakhein taake floor ke andar spawn na ho
                savedPos.y += 1.0f; 

                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                
                player.transform.position = savedPos;
                
                // CRITICAL FIX: Unity Physics engine ko foran update karo warna player neechay gir jata hai
                Physics.SyncTransforms();
                
                if (cc != null) cc.enabled = true;
                Debug.Log("Player respawned safely at: " + savedPos);
            }
        }

        // WORLD STATE: Remove items that were already picked up or used!
        if (!string.IsNullOrEmpty(data.collectedItemsCSV))
        {
            string[] itemsToDestroy = data.collectedItemsCSV.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string itemName in itemsToDestroy)
            {
                if (itemName == data.droppedItemName) continue; // Drop hui cheez ko delete nahi karna!
                
                GameObject oldItem = GameObject.Find(itemName);
                if (oldItem != null)
                {
                    Destroy(oldItem);
                    Debug.Log("[WorldState] Destroyed already collected item: " + itemName);
                }
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
        // Foran fade out shuru karo
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

        // 1. Hide Text (User requested to delete text)
        if (spookyText != null)
        {
            spookyText.gameObject.SetActive(false);
        }

        // 2. Show the new Cinematic Image!
        if (deathImage != null)
        {
            deathImage.gameObject.SetActive(true);
            
            // Start transparent and fade the image in for cinematic effect
            Color c = deathImage.color;
            c.a = 0f;
            deathImage.color = c;
            
            float imageFade = 0f;
            while(imageFade < 1f)
            {
                imageFade += Time.deltaTime * 0.5f; // Slow fade
                c.a = imageFade;
                deathImage.color = c;
                yield return null;
            }
        }

        yield return new WaitForSeconds(4f); // Read time

        // USER REQUEST: Sirf basement mein hi spawn karna hai dayroom mein nahi!
        Transform chosenRespawn = basementRespawn;
        
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

        // Respawn reload: intro cinematic/projector must NOT play again
        IntroState.skipIntro = true;

        // Reload Scene (It will auto-place the player and dropped item in Start)
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
