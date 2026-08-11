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

    public void StartNightmarePhase()
    {
        if (currentPhase == GamePhase.Awakening)
        {
            currentPhase = GamePhase.Nightmare;
            Debug.Log("[Progression] Phase Changed: The Nightmare Begins!");

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
