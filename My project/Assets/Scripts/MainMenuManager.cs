using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public void StartGame()
    {
        PlayClickSound();
        IntroState.ResetForNewGame(); // fresh game = intro plays again
        // Nayi game shuru karte hue purani save delete kar do taake fresh start ho
        string savePath = Application.persistentDataPath + "/AsylumSave.json";
        if (System.IO.File.Exists(savePath)) 
        {
            System.IO.File.Delete(savePath);
            Debug.Log("Old save deleted for New Game.");
        }
        
        SceneManager.LoadScene("neww");
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
        // Shuru mein sirf Main Menu nazar aana chahiye
        if (mainMenuScreen != null) mainMenuScreen.SetActive(true);
        if (settingsScreen != null) settingsScreen.SetActive(false);

        if (resumeButton != null)
        {
            string savePath = Application.persistentDataPath + "/AsylumSave.json";
            resumeButton.interactable = System.IO.File.Exists(savePath);
        }
    }

    // Ye function Quit button ke liye hai
    public void QuitGame()
    {
        PlayClickSound();
        Debug.Log("Quit Game command received!");
        Application.Quit();
    }

    // Ye function Resume button ke liye hai
    public void ResumeGame()
    {
        PlayClickSound();
        Debug.Log("Resume Game command received! Loading last save...");
        SceneManager.LoadScene("neww");
    }

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
