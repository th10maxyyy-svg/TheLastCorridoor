public interface IInteractable
{
    string GetInteractPrompt();
    void Interact(PlayerStats playerStats);
}
