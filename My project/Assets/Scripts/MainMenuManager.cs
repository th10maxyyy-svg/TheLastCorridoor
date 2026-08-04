using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    // Ye function Play button ke liye hai
    public void StartGame()
    {
        // "SampleScene" ko load karega (ensure karein ke apke main game scene ka naam yahi ho)
        SceneManager.LoadScene("SampleScene");
    }

    // Ye function Quit button ke liye hai
    public void QuitGame()
    {
        Debug.Log("Quit Game command received!");
        Application.Quit();
    }
}
