using UnityEngine;

public class GameManager : MonoBehaviour
{
    private bool isGameOver = false;

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        
        isGameOver = true;
        Debug.Log("Game Over triggered in GameManager!");
        
        // Pause the game time
        Time.timeScale = 0f;
        
        // Unlock mouse cursor so player can click buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RestartGame()
    {
        isGameOver = false;
        Time.timeScale = 1f;
        // Reload current active scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );
    }
}
