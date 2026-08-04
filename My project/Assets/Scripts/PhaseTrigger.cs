using UnityEngine;

public class PhaseTrigger : MonoBehaviour
{
    private bool hasTriggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        // Check if Player crossed the trigger
        if (other.CompareTag("Player") || other.GetComponent<PlayerStats>() != null)
        {
            ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
            if (pm != null)
            {
                pm.StartNightmarePhase(); // Start enemy hunt!
                hasTriggered = true;
                
                // Hide this trigger so it doesn't fire again
                gameObject.SetActive(false);
            }
        }
    }
}
