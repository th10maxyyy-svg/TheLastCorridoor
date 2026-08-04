using UnityEngine;
using UnityEngine.UI; // Optional for UI Slider references

public class PlayerStats : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDepletionRate = 20f; // stamina used per second when sprinting
    [SerializeField] private float staminaRegenRate = 15f;      // stamina recovered per second
    [SerializeField] private float regenDelay = 1.5f;          // seconds to wait before regenerating stamina after using it
    private float currentStamina;
    private float regenTimer;

    [Header("Optional UI References")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider staminaSlider;

    private bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        UpdateUI();
    }

    void Update()
    {
        HandleStaminaRegen();
        UpdateUI();
    }

    private void HandleStaminaRegen()
    {
        if (regenTimer > 0)
        {
            regenTimer -= Time.deltaTime;
        }
        else if (currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
        }
    }

    public void UseStamina(float deltaTime)
    {
        currentStamina -= staminaDepletionRate * deltaTime;
        currentStamina = Mathf.Max(currentStamina, 0f);
        regenTimer = regenDelay; // Reset regen timer
    }

    public bool HasStamina()
    {
        return currentStamina > 0.5f; // Small buffer to prevent stuttering sprint at 0
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        Debug.Log($"Player took damage: {amount}. Current health: {currentHealth}");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("Player has died!");
        
        // Find the GameManager and trigger Game Over
        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.TriggerGameOver();
        }
    }

    private void UpdateUI()
    {
        if (healthSlider != null)
        {
            healthSlider.value = currentHealth / maxHealth;
        }
        if (staminaSlider != null)
        {
            staminaSlider.value = currentStamina / maxStamina;
        }
    }

    // Getters for other scripts
    public float GetHealth() => currentHealth;
    public float GetStamina() => currentStamina;
    public bool IsDead() => isDead;
}
