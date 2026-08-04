using UnityEngine;
using UnityEngine.UI;

public class PlayerStats : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDepletionRate = 20f; 
    [SerializeField] private float staminaRegenRate = 15f;      
    [SerializeField] private float regenDelay = 1.5f;          
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
        regenTimer = regenDelay; 
    }

    public bool HasStamina()
    {
        return currentStamina > 0.5f; 
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        Debug.Log("Player took damage: " + amount + ". Current health: " + currentHealth);

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
        
        GameManager gameManager = FindAnyObjectByType<GameManager>();
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

    public void LoadStats(float health, float stamina)
    {
        currentHealth = health;
        currentStamina = stamina;
        isDead = currentHealth <= 0f;
        UpdateUI();
    }

    public float GetHealth() => currentHealth;
    public float GetStamina() => currentStamina;
    public bool IsDead() => isDead;
}
