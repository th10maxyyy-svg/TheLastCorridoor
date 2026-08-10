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
        // OLD CODE: Shuru mein Enemy ko disable rakhein
        // NAYA CODE: Ab hum enemy ko disable nahi karte, balkay EnemyAI script usay khud "Dormant" rakhta hai.
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
