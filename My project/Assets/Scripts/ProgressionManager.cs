using UnityEngine;
using UnityEngine.Events;

public class ProgressionManager : MonoBehaviour
{
    public enum GamePhase { Awakening, Nightmare, Escape }
    
    [Header("Current Phase")]
    public GamePhase currentPhase = GamePhase.Awakening;

    [Header("References")]
    [SerializeField] private EnemyAI enemyAI;
    [SerializeField] private GameObject startRoomDoor; // The door locking the player in

    [Header("Events")]
    public UnityEvent OnNightmarePhaseStarted;

    void Start()
    {
        // WORLD STATE CHECK: Agar nightmare pehle hi shuru ho chuka tha to Reload par usay wapas jaga do!
        if (SaveManager.instance != null && SaveManager.instance.currentSaveData.isNightmareActive)
        {
            currentPhase = GamePhase.Nightmare;
            if (enemyAI != null)
            {
                enemyAI.gameObject.SetActive(true);
                PlayerController player = FindAnyObjectByType<PlayerController>();
                Vector3 targetPos = player != null ? player.transform.position : transform.position;
                enemyAI.ForceAwaken(targetPos);
            }
            Debug.Log("[Progression] World State: Nightmare Resumed!");
        }
    }

    public void StartNightmarePhase()
    {
        if (currentPhase == GamePhase.Awakening)
        {
            currentPhase = GamePhase.Nightmare;
            Debug.Log("[Progression] Phase Changed: The Nightmare Begins!");

            if (SaveManager.instance != null)
            {
                SaveManager.instance.currentSaveData.isNightmareActive = true;
                SaveManager.instance.SaveWorldState();
            }

            // Enemy ko awake karein (dormant se nikalein)
            if (enemyAI != null)
            {
                enemyAI.gameObject.SetActive(true);
                
                // Naya logic: EnemyAI ko force awaken karo!
                PlayerController player = FindAnyObjectByType<PlayerController>();
                Vector3 targetPos = player != null ? player.transform.position : transform.position;
                enemyAI.ForceAwaken(targetPos);
            }

            OnNightmarePhaseStarted?.Invoke();
        }
    }

    public void StartEscapePhase()
    {
        if (currentPhase == GamePhase.Nightmare)
        {
            currentPhase = GamePhase.Escape;
            Debug.Log("[Progression] Phase Changed: Final Escape!");
            // Yahan final escape sequence trigger hoga
        }
    }
}
