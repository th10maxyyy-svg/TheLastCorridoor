using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
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

    [Header("Death Messages (shown on the black screen)")]
    [Tooltip("1st death (2 lives left)")]
    public string firstDeathTitle = "HE LET YOU LIVE.";
    public string firstDeathLine = "He is not done playing with you yet...";
    [Tooltip("2nd death (1 life left)")]
    public string secondDeathTitle = "YOU WON'T BE SO LUCKY AGAIN.";
    public string secondDeathLine = "Next time, he keeps you.";

    private string msgTitle, msgLine, msgLives;
    private float msgStart = -1f;
    private GUIStyle msgTitleStyle, msgLineStyle, msgLivesStyle;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
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
            PlacePlayer();
            StartCoroutine(RestoreWorldState());
        }

        // After a death-respawn reload (or Resume) make sure the player is never left frozen
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

    private void PlacePlayer()
    {
        SaveData data = SaveManager.instance.currentSaveData;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        Vector3 savedPos = SaveManager.instance.GetVector3(data.playerPosition);
        if (savedPos == Vector3.zero) return;

        // Zameen se thora sa oopar rakhein taake floor ke andar spawn na ho
        savedPos.y += 1.0f;

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.position = savedPos;
        if (SaveManager.instance.TryGetPlayerRotation(out Quaternion rot))
        {
            player.transform.rotation = Quaternion.Euler(0f, rot.eulerAngles.y, 0f);
        }

        // CRITICAL FIX: Unity Physics engine ko foran update karo warna player neechay gir jata hai
        Physics.SyncTransforms();

        if (cc != null) cc.enabled = true;
        Debug.Log("Player respawned safely at: " + savedPos);
    }

    private IEnumerator RestoreWorldState()
    {
        // Wait one frame so every puzzle's Start() (which hides its reward items) has already run
        yield return null;

        SaveManager sm = SaveManager.instance;
        if (sm == null) yield break;
        SaveData data = sm.currentSaveData;

        // 1. Items lying on the floor (dropped by the player, or released from a smashed jar)
        foreach (KeyValuePair<string, Vector3> entry in sm.GetDroppedItems())
        {
            GameObject item = sm.FindItem(entry.Key);
            if (item == null) continue;

            item.transform.SetParent(null);
            item.SetActive(true);
            item.transform.position = entry.Value;

            foreach (Collider c in item.GetComponentsInChildren<Collider>()) c.enabled = true;

            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }

        // 2. Item that was in the player's hand when the game was closed
        string heldId = data.heldItemName;
        GameObject heldItem = string.IsNullOrEmpty(heldId) ? null : sm.FindItem(heldId);

        sm.MarkWorldRestored();

        if (heldItem != null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Inventory inv = player != null ? player.GetComponent<Inventory>() : null;
            if (inv != null) inv.PickUpItem(heldItem, true);
        }

        // 3. Remove items that were already picked up / used (after steps 1 & 2, so items inside a jar are rescued first)
        if (!string.IsNullOrEmpty(data.collectedItemsCSV))
        {
            foreach (string itemId in data.collectedItemsCSV.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (sm.ShouldItemExistInWorld(itemId)) continue;

                GameObject oldItem = sm.FindItem(itemId);
                if (oldItem != null)
                {
                    Destroy(oldItem);
                    Debug.Log("[WorldState] Destroyed already collected item: " + itemId);
                }
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

        // 1. Hide the old scene text; the death message is drawn on top of the black screen (see OnGUI)
        if (spookyText != null)
        {
            spookyText.gameObject.SetActive(false);
        }
        bool lastLife = livesRemaining <= 1;
        msgTitle = lastLife ? secondDeathTitle : firstDeathTitle;
        msgLine = lastLife ? secondDeathLine : firstDeathLine;
        msgLives = lastLife ? "LAST LIFE" : livesRemaining + " LIVES LEFT";
        msgStart = Time.unscaledTime;

        // 2. Show the new Cinematic Image!
        if (deathImage != null)
        {
            deathImage.gameObject.SetActive(true);

            // Start transparent and fade the image in for cinematic effect
            Color c = deathImage.color;
            c.a = 0f;
            deathImage.color = c;

            float imageFade = 0f;
            while (imageFade < 1f)
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

        // Save the new respawn position (this also stores where the dropped items ended up)
        if (SaveManager.instance != null)
        {
            if (chosenRespawn != null) SaveManager.instance.SavePlayerTransform(chosenRespawn.position, chosenRespawn.rotation);
            else SaveManager.instance.SaveWorldState();
        }

        // Respawn reload: intro cinematic/projector must NOT play again
        IntroState.skipIntro = true;

        // Reload Scene (It will auto-place the player and dropped items in Start)
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    // ------------------------------------------------------------------ death message (1st and 2nd death)
    private void OnGUI()
    {
        if (msgStart < 0f || string.IsNullOrEmpty(msgTitle)) return;
        float t = Time.unscaledTime - msgStart;
        float k = Screen.height / 1080f;
        if (msgTitleStyle == null)
        {
            msgTitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
            msgLineStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic, wordWrap = true };
            msgLivesStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
        msgTitleStyle.fontSize = Mathf.RoundToInt(64 * k);
        msgLineStyle.fontSize = Mathf.RoundToInt(30 * k);
        msgLivesStyle.fontSize = Mathf.RoundToInt(22 * k);

        GUI.depth = -1000;
        float w = Screen.width * 0.9f, x = (Screen.width - w) * 0.5f, cy = Screen.height * 0.5f;

        // the title types itself in, letter by letter, with a slight flicker
        int shown = Mathf.Clamp(Mathf.FloorToInt(t * 22f), 0, msgTitle.Length);
        float flicker = (t < 1.5f && Random.value < 0.08f) ? 0.4f : 1f;
        DrawShadowed(new Rect(x, cy - 120f * k, w, 110f * k), msgTitle.Substring(0, shown), msgTitleStyle,
                     new Color(0.72f, 0.06f, 0.05f, flicker));

        float a2 = Mathf.Clamp01((t - msgTitle.Length / 22f - 0.3f) / 0.8f);
        if (a2 > 0f)
        {
            DrawShadowed(new Rect(x, cy, w, 50f * k), msgLine, msgLineStyle, new Color(0.88f, 0.84f, 0.76f, a2));
            DrawShadowed(new Rect(x, cy + 70f * k, w, 34f * k), msgLives, msgLivesStyle, new Color(0.72f, 0.06f, 0.05f, a2 * 0.9f));
        }
    }

    private static void DrawShadowed(Rect r, string text, GUIStyle style, Color c)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, c.a * 0.85f);
        GUI.Label(new Rect(r.x + 3f, r.y + 3f, r.width, r.height), text, style);
        GUI.color = c;
        style.normal.textColor = Color.white;
        GUI.Label(r, text, style);
        GUI.color = old;
    }
}
