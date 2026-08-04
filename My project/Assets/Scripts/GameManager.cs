using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject gameOverPanel;

    private bool isGameOver = false;

    void Start()
    {
        Time.timeScale = 1f;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;

        isGameOver = true;
        Debug.Log("Game Over triggered!");

        // Game pause karo
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Game Over panel show karo
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
    }

    public void RestartGame()
    {
        isGameOver = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public bool IsGameOver() => isGameOver;
}
