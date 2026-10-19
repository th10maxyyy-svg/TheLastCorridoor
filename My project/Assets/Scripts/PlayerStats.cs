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
    private float forceBreatheTimer = 0f; // NEW: Jab stamina bhara ho lekin darr se saans phoolay

    private bool isDead = false;

    [Header("Voice & Dialogue")]
    [SerializeField] private AudioSource voiceAudioSource;
    [SerializeField] private AudioClip damageGruntSound; // NEW: Sirf aah/ouch wali aawaz
    [SerializeField] private AudioClip damageVoiceSound; // Jab chot lagay aur bolna ho

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
        
        // AUTO-FIX: Create audio source for breathing if it's missing!
        if (breathingAudioSource == null)
        {
            breathingAudioSource = gameObject.AddComponent<AudioSource>();
            breathingAudioSource.spatialBlend = 0f; // 2D sound
        }

        // AUTO-FIX: Create audio source for voice/dialogue!
        if (voiceAudioSource == null)
        {
            voiceAudioSource = gameObject.AddComponent<AudioSource>();
            voiceAudioSource.spatialBlend = 0f; // 2D sound
        }

        UpdateUI();
        
        if (bloodOverlay != null)
        {
            Color c = bloodOverlay.color;
            c.a = 0f;
            bloodOverlay.color = c;
            bloodOverlay.raycastTarget = false; // GUARANTEED FIX: Khoon ka nishaan UI clicks block nahi karega!
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

    // NEW METHOD: Kisi bhi event se saans phulane ke liye (Cinematic, Damage, Escape)
    public void TriggerHeavyBreathing(float duration)
    {
        if (isDead) return;
        forceBreatheTimer = Mathf.Max(forceBreatheTimer, duration);
    }

    private void HandleBreathing()
    {
        if (heavyBreathingSound == null)
        {
            if (forceBreatheTimer > 0) Debug.LogWarning("PlayerStats: Saans ki aawaz play karni thi par 'Heavy Breathing Sound' mein koi file nahi dali hui!");
            return;
        }

        if (breathingAudioSource == null || isDead) return;

        if (forceBreatheTimer > 0)
        {
            forceBreatheTimer -= Time.deltaTime;
        }

        // Jab stamina 30% se kam ho, YA phir koi darawna scene hua ho (forceBreatheTimer > 0)
        bool shouldBreathe = (currentStamina <= maxStamina * 0.3f) || (forceBreatheTimer > 0);
        bool shouldStop = (currentStamina >= maxStamina * 0.7f) && (forceBreatheTimer <= 0);

        if (!isBreathing && shouldBreathe)
        {
            isBreathing = true;
            breathingAudioSource.clip = heavyBreathingSound;
            breathingAudioSource.loop = true;
            breathingAudioSource.Play();
        }
        else if (isBreathing && shouldStop)
        {
            isBreathing = false;
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

    /// <summary>True during the ending cinematic - nothing may kill the player while the story wraps up.</summary>
    [HideInInspector] public bool isInvulnerable = false;

    public void TakeDamage(float amount)
    {
        if (isDead || isInvulnerable) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        Debug.Log("Player took damage: " + amount + ". Current health: " + currentHealth);

        // NAYA CODE: Pehle Dard ki aawaz (Grunt), phir 1 second baad Dialogue!
        if (voiceAudioSource != null && !voiceAudioSource.isPlaying)
        {
            StartCoroutine(PlayDamageVoiceSequence());
        }

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

    private System.Collections.IEnumerator PlayDamageVoiceSequence()
    {
        // 1. Play pain grunt ("Ouch!")
        if (damageGruntSound != null)
        {
            voiceAudioSource.PlayOneShot(damageGruntSound);
            // Wait for half a second before speaking
            yield return new WaitForSeconds(0.6f);
        }

        // 2. Play the dialogue ("Where am I stuck?!")
        if (damageVoiceSound != null && !isDead)
        {
            voiceAudioSource.PlayOneShot(damageVoiceSound);
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
