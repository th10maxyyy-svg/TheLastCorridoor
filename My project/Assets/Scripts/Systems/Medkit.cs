using UnityEngine;

/// <summary>
/// MEDKIT: press E to use it on the spot and get health back (not when health is already full).
/// Each medkit can be used once per game - it is remembered in the save (flag Medkit_&lt;id&gt;).
/// Placed by the menu: The Last Corridor / Setup Game Extras.
/// </summary>
public class Medkit : MonoBehaviour, IInteractable
{
    public float healAmount = 40f;
    public AudioClip useSound;

    private string Flag => "Medkit_" + SaveManager.GetSceneObjectId(transform);

    void Start()
    {
        if (SaveManager.instance != null && SaveManager.instance.HasFlag(Flag)) Destroy(gameObject);
    }

    public string GetInteractPrompt()
    {
        PlayerStats ps = FindAnyObjectByType<PlayerStats>();
        if (ps != null && ps.GetHealth() >= ps.MaxHealth - 0.5f) return "Medkit  (health is full)";
        return "Press E to use Medkit  (+" + Mathf.RoundToInt(healAmount) + " health)";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (playerStats == null || playerStats.IsDead()) return;
        if (playerStats.GetHealth() >= playerStats.MaxHealth - 0.5f)
        {
            InteractionRaycast.Notify(playerStats, "You are not hurt. Keep it for later.", 2f);
            return;
        }
        playerStats.Heal(healAmount);
        if (SaveManager.instance != null) SaveManager.instance.SetFlag(Flag);
        if (useSound != null) AudioSource.PlayClipAtPoint(useSound, transform.position, 0.8f);
        InteractionRaycast.Notify(playerStats, "Used a medkit  (+" + Mathf.RoundToInt(healAmount) + " health)", 2.5f);
        Destroy(gameObject);
    }
}
