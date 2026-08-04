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
        // Shuru mein Enemy ko disable rakhein
        if (enemyAI != null)
        {
            enemyAI.gameObject.SetActive(false);
        }
        else
        {
            EnemyAI foundEnemy = FindAnyObjectByType<EnemyAI>();
            if (foundEnemy != null)
            {
                enemyAI = foundEnemy;
                enemyAI.gameObject.SetActive(false);
            }
        }
    }

    // Yeh function tab call hoga jab player Start Room se bahar aayega
    public void StartNightmarePhase()
    {
        if (currentPhase == GamePhase.Awakening)
        {
            currentPhase = GamePhase.Nightmare;
            Debug.Log("[Progression] Phase Changed: The Nightmare Begins!");

            // Enemy ko active karein
            if (enemyAI != null)
            {
                // Enemy ko player se thora door spawn karne ka logic pehle se CorridorGenerator me hai
                enemyAI.gameObject.SetActive(true);
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
