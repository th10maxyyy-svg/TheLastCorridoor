using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("UI References")]
    [SerializeField] private GameObject gameOverPanel;

    private bool isGameOver = false;

    [Header("Audio Settings")]
    public AudioSource gameManagerAudio;
    public AudioClip gameOverSound;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

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

        // Play the game over sound
        if (gameManagerAudio != null && gameOverSound != null)
        {
            gameManagerAudio.PlayOneShot(gameOverSound);
        }

        // Time.timeScale = 0f; // Ab time nahi rukega, background real-time chalega!
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        StartCoroutine(GameOverSequence());
    }

    private System.Collections.IEnumerator GameOverSequence()
    {
        // Wait for player to fall down and enemy to vanish
        yield return new WaitForSeconds(2.5f); 
        
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            CanvasGroup cg = gameOverPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                while (cg.alpha < 1f)
                {
                    cg.alpha += Time.deltaTime * 0.5f; // Slow cinematic fade in
                    yield return null;
                }
            }
        }
    }

    public void RestartGame()
    {
        isGameOver = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public bool IsGameOver() => isGameOver;
}
