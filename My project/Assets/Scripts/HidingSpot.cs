using UnityEngine;

public class HidingSpot : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform hidePosition;
    [SerializeField] private Transform exitPosition;

    public string GetInteractPrompt()
    {
        return "Press E to Hide";
    }

    public void Interact(PlayerStats player)
    {
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null && !pc.isHidden)
        {
            if (hidePosition == null || exitPosition == null)
            {
                Debug.LogWarning("HidingSpot: hidePosition ya exitPosition assign nahi hai!");
                return;
            }
            
            pc.EnterHiding(hidePosition, exitPosition);
        }
    }
}
