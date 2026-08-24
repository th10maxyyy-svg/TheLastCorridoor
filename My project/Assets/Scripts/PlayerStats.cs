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

    [Header("Immersive Audio (Breathing)")]
    [SerializeField] private AudioSource breathingAudioSource;
    [SerializeField] private AudioClip heavyBreathingSound;
    private bool isBreathing = false;

    private bool isDead = false;

    [Header("Damage UI")]
    [Tooltip("Screen par blood dikhane wala image")]
    public UnityEngine.UI.Image bloodOverlay;
    [Tooltip("Kitne seconds tak khoon screen par rahay ga")]
    public float bloodDisplayTime = 6f;
    [Tooltip("Khoon kitni tezi se fade out hoga")]
    public float bloodFadeSpeed = 0.5f;

    private float currentBloodAlpha = 0f;
    private float bloodTimer = 0f;

    void Start()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        UpdateUI();
        
        if (bloodOverlay != null)
        {
            Color c = bloodOverlay.color;
            c.a = 0f;
            bloodOverlay.color = c;
        }
    }

    void Update()
    {
        HandleStaminaRegen();
        HandleBreathing();

        // Handle blood overlay fading
        if (bloodOverlay != null)
        {
            if (bloodTimer > 0)
            {
                bloodTimer -= Time.deltaTime;
                // Khoon fauran (instantly) screen par aata hai
                currentBloodAlpha = Mathf.Lerp(currentBloodAlpha, 1f, Time.deltaTime * 15f);
            }
            else
            {
                // Timer khatam hone par ahista ahista fade out hota hai
                currentBloodAlpha = Mathf.Lerp(currentBloodAlpha, 0f, Time.deltaTime * bloodFadeSpeed);
            }

            Color c = bloodOverlay.color;
            c.a = currentBloodAlpha;
            bloodOverlay.color = c;
        }

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

    private void HandleBreathing()
    {
        if (breathingAudioSource == null || heavyBreathingSound == null || isDead) return;

        // Jab stamina 30% se kam ho, saans phulna shuru
        if (!isBreathing && currentStamina <= maxStamina * 0.3f)
        {
            isBreathing = true;
            breathingAudioSource.clip = heavyBreathingSound;
            breathingAudioSource.loop = true;
            breathingAudioSource.Play();
        }
        // Jab stamina 70% tak wapis aa jaye, saans normal ho jaye
        else if (isBreathing && currentStamina >= maxStamina * 0.7f)
        {
            isBreathing = false;
            // Dheere dheere stop karne ke liye hum volume bhi kam kar sakte hain, 
            // par abhi ke liye directly stop kar dete hain
            breathingAudioSource.Stop();
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

        // Screen par khoon ka chheenta (splatter) show karein
        if (bloodOverlay != null)
        {
            bloodTimer = bloodDisplayTime; // Ye khoon ko screen par rok kar rakhega
        }

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
        
        if (breathingAudioSource != null && breathingAudioSource.isPlaying)
        {
            breathingAudioSource.Stop();
        }
        
        // Trigger cinematic fall
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.TriggerDeathCinematic();
        }
        
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
