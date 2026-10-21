using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Tooltip("Game wale scene ka naam")]
    public string gameSceneName = "neww";

    public void StartGame()
    {
        PlayClickSound();
        IntroState.ResetForNewGame(); // fresh game = intro plays again

        // Nayi game shuru karte hue purani save delete kar do taake fresh start ho
        // (SaveManager memory mein bhi zinda ho sakta hai, us ka data bhi reset karo)
        if (SaveManager.instance != null)
        {
            SaveManager.instance.DeleteSave();
        }

        // The game scene's SaveManager falls back to the .bak backup when the main save file is missing, so a "new game" that
        // deleted only the main file still came back as the OLD game (it behaved like RESUME). Remove every copy.
        string main = SaveManager.SaveFilePath;
        string[] files = { main, SaveManager.BackupPathFor(main), main + ".tmp" };
        foreach (string f in files)
        {
            try { if (System.IO.File.Exists(f)) System.IO.File.Delete(f); }
            catch (System.Exception e) { Debug.LogWarning("Could not delete old save file " + f + ": " + e.Message); }
        }
        IntroState.skipIntro = false;
        EnemyRestore.RestoreOnNextLoad = false;   // a new game never restores the enemy's old position
        Debug.Log("Old save deleted for New Game.");

        SceneManager.LoadScene(gameSceneName);
    }

    [Header("UI Screens")]
    public GameObject mainMenuScreen;
    public GameObject settingsScreen;

    [Header("UI Buttons")]
    public UnityEngine.UI.Button resumeButton;

    [Header("Audio Settings")]
    [Tooltip("Is dabbe mein menu wala canvas ya koi bhi game object daalein jis par AudioSource ho")]
    public AudioSource uiAudioSource;
    [Tooltip("Isme click ki sound daalein")]
    public AudioClip clickSound;

    private void PlayClickSound()
    {
        if (uiAudioSource != null && clickSound != null)
        {
            uiAudioSource.PlayOneShot(clickSound);
        }
    }

    void Start()
    {
        // Menu mein cursor hamesha nazar aaye (game se wapas aane par locked reh jata tha)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        // Shuru mein sirf Main Menu nazar aana chahiye
        if (mainMenuScreen != null) mainMenuScreen.SetActive(true);
        if (settingsScreen != null) settingsScreen.SetActive(false);

        if (resumeButton != null)
        {
            resumeButton.interactable = System.IO.File.Exists(SaveManager.SaveFilePath);
        }
    }

    // Ye function Quit button ke liye hai
    public void QuitGame()
    {
        PlayClickSound();
        Debug.Log("Quit Game command received!");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // Application.Quit() does nothing inside the Unity Editor
#else
        Application.Quit();
#endif
    }

    // Ye function Resume button ke liye hai
    public void ResumeGame()
    {
        if (!System.IO.File.Exists(SaveManager.SaveFilePath))
        {
            StartGame(); // nothing to resume
            return;
        }

        PlayClickSound();
        Debug.Log("Resume Game command received! Loading last save...");

        // BUG FIX: Resume par intro cinematic / projector video dobara nahi chalna chahiye
        IntroState.skipIntro = true;
        EnemyRestore.RestoreOnNextLoad = true; // FR_40: the enemy comes back where he was at the last checkpoint

        // Make sure the save is (re)read from disk
        if (SaveManager.instance != null) SaveManager.instance.LoadGame();

        SceneManager.LoadScene(gameSceneName);
    }

    // LOAD GAME / ACHIEVEMENTS / OPTIONS buttons (they open the panels of GameUI)
    public void OpenLoadGame() { PlayClickSound(); GameUI.OpenFromMenu(0); }
    public void OpenAchievements() { PlayClickSound(); GameUI.OpenFromMenu(1); }
    public void OpenOptions() { PlayClickSound(); GameUI.OpenFromMenu(2); }

    // Ye function Settings kholne ke liye hai
    public void OpenSettings()
    {
        PlayClickSound();
        if (mainMenuScreen != null) mainMenuScreen.SetActive(false);
        if (settingsScreen != null) settingsScreen.SetActive(true);
    }

    // Ye function wapas Main Menu jane ke liye (Back Button)
    public void CloseSettings()
    {
        PlayClickSound();
        if (settingsScreen != null) settingsScreen.SetActive(false);
        if (mainMenuScreen != null) mainMenuScreen.SetActive(true);
    }
}
