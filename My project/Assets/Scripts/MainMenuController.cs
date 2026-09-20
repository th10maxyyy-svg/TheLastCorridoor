using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Management")]
    [Tooltip("The exact name of your main game scene (e.g., 'SampleScene')")]
    public string gameSceneName = "SampleScene";

    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject optionsPanel;

    [Header("Audio")]
    public AudioSource uiAudioSource;
    public AudioClip hoverSound;
    public AudioClip clickSound;
    public AudioClip gameStartSound;

    void Start()
    {
        // Make sure mouse is visible and unlocked in main menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    // --- BUTTON FUNCTIONS ---

    public void PlayGame()
    {
        // Reset the Intro Cinematic flag for a fresh game!
        PlayerPrefs.SetInt("HasSeenIntro", 0);
        PlayerPrefs.Save();

        PlayClickSound();
        if (gameStartSound != null && uiAudioSource != null)
        {
            uiAudioSource.PlayOneShot(gameStartSound);
        }
        
        // Start a coroutine to wait for the scary sound to finish before loading the scene
        StartCoroutine(LoadGameSequence());
    }

    private IEnumerator LoadGameSequence()
    {
        // Hide the UI for a cinematic start
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        
        // Wait 1.5 seconds for dramatic effect / sound to play
        yield return new WaitForSeconds(1.5f);
        
        // Load the main game!
        SceneManager.LoadScene(gameSceneName);
    }

    public void ResumeGame()
    {
        PlayClickSound();
        Debug.Log("Resuming Game... (Loading last save point)");
        // TODO: Jab hum save system banayenge to yahan last save load hoga. Abhi ke liye ye normal play ki tarah scene load karega.
        StartCoroutine(LoadGameSequence());
    }

    public void OpenSettings()
    {
        PlayClickSound();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    public void OpenOptions()
    {
        OpenSettings(); // Just in case you still use this name
    }

    public void CloseOptions()
    {
        PlayClickSound();
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    public void QuitGame()
    {
        PlayClickSound();
        Debug.Log("Quitting Game...");
        Application.Quit();
    }

    // --- AUDIO FUNCTIONS ---
    // Link these to the "Event Trigger" component (PointerEnter) on your buttons
    public void PlayHoverSound()
    {
        if (uiAudioSource != null && hoverSound != null)
        {
            uiAudioSource.PlayOneShot(hoverSound);
        }
    }

    private void PlayClickSound()
    {
        if (uiAudioSource != null && clickSound != null)
        {
            uiAudioSource.PlayOneShot(clickSound);
        }
    }
}
