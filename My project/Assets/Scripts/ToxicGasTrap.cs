using UnityEngine;
using System.Collections;

public class ToxicGasTrap : MonoBehaviour
{
    [Header("Trap Settings")]
    [Tooltip("Total seconds before the player suffocates and dies")]
    public float deathTimer = 60f;
    [Tooltip("Amount of damage per second once gas starts getting heavy")]
    public float damagePerSecond = 5f;

    [Header("References")]
    public DoorController entranceDoor;
    [Tooltip("The fuse box that stops the gas (found automatically in the same room if empty)")]
    public FuseBoxPuzzle fuseBox;
    public GameObject gasParticleSystem; // The green fog/gas visual
    public AudioClip gasHissingSound;
    public AudioClip hallucinationSound; // Random spooky noises

    [Header("Hallucination Settings")]
    public GameObject fakeJokerPrefab;
    [Tooltip("How close the fake Joker spawns to the player")]
    public float spawnDistance = 2.5f;

    [Header("Visual Effects (Lights & Explosion)")]
    [Tooltip("Lights that will explode/turn off when trap starts")]
    public Light[] roomLights;
    public AudioClip explosionSound;
    public ParticleSystem sparksParticle; // Optional sparks on explode

    private bool isTrapActive = false;
    private PlayerStats playerStats;
    private Camera mainCam;
    private ParticleSystem.EmissionModule gasEmission;
    private bool hasGasEmission = false;
    private float originalEmissionRate = 0f;
    private AudioSource myAudioSource;
    private string saveFlag;

    private void Start()
    {
        // WORLD STATE: agar player pehle hi gas saaf kar chuka hai to trap dobara na chale
        saveFlag = "GasTrapCleared_" + SaveManager.GetSceneObjectId(transform);
        if (fuseBox == null && transform.parent != null) fuseBox = transform.parent.GetComponentInChildren<FuseBoxPuzzle>(true);
        if (SaveManager.instance != null && SaveManager.instance.HasFlag(saveFlag))
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isTrapActive && other.CompareTag("Player"))
        {
            StartTrap(other.gameObject);
        }
    }

    public void StartTrap(GameObject player)
    {
        isTrapActive = true;
        playerStats = player.GetComponent<PlayerStats>();
        mainCam = player.GetComponentInChildren<Camera>();

        // Lock the door behind them (public API instead of reflection on private fields)
        if (entranceDoor != null)
        {
            entranceDoor.TrapLock();
        }

        // --- NEW: Lights Explode! ---
        if (myAudioSource == null) myAudioSource = gameObject.AddComponent<AudioSource>();

        if (explosionSound != null) myAudioSource.PlayOneShot(explosionSound);
        if (sparksParticle != null) sparksParticle.Play();

        if (roomLights != null)
        {
            foreach (Light l in roomLights)
            {
                if (l != null) l.enabled = false;
            }
        }

        // Start visuals and audio
        if (gasParticleSystem != null)
        {
            gasParticleSystem.SetActive(true);
            ParticleSystem ps = gasParticleSystem.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                gasEmission = ps.emission;
                originalEmissionRate = gasEmission.rateOverTimeMultiplier;
                hasGasEmission = true;
            }
        }

        if (gasHissingSound != null)
        {
            myAudioSource.clip = gasHissingSound;
            myAudioSource.loop = true;
            myAudioSource.Play();
        }

        // Start the death sequence
        StartCoroutine(GasTrapRoutine());
        StartCoroutine(HallucinationRoutine());

        // Hide the trigger so it doesn't trigger twice
        Collider trigger = GetComponent<Collider>();
        if (trigger != null) trigger.enabled = false;
    }

    public void StopTrap()
    {
        if (!isTrapActive) return;
        isTrapActive = false;

        Debug.Log("ToxicGasTrap: Gas is clearing! Fans turned on.");

        if (myAudioSource != null && myAudioSource.isPlaying) myAudioSource.Stop();

        // Player ab bahar ja sakta hai
        if (entranceDoor != null) entranceDoor.ReleaseTrapLock();

        if (SaveManager.instance != null) SaveManager.instance.SetFlag(saveFlag);

        // NAYA: Gas fauran (instantly) gayab nahi hogi, ahista ahista clear hogi!
        StartCoroutine(ClearGasRoutine());
    }

    private IEnumerator ClearGasRoutine()
    {
        if (hasGasEmission)
        {
            float fadeTime = 5f;
            float currentRate = originalEmissionRate;

            // Ahista ahista emission kam karein
            while (fadeTime > 0)
            {
                fadeTime -= Time.deltaTime;
                currentRate = Mathf.Lerp(currentRate, 0f, Time.deltaTime * 2f);
                gasEmission.rateOverTimeMultiplier = currentRate;
                yield return null;
            }

            gasEmission.rateOverTimeMultiplier = 0f;
        }

        // Thora wait karein taake hawa mein jo gas bachi hai wo khatam ho jaye
        yield return new WaitForSeconds(6f);

        if (gasParticleSystem != null) gasParticleSystem.SetActive(false);
    }

    private bool FusesDone => fuseBox != null && fuseBox.IsSolved;

    private IEnumerator GasTrapRoutine()
    {
        float timer = deathTimer;
        float tick = 0f;

        // BUG FIX: damage used to be applied (and logged, with a grunt + blood splash) EVERY FRAME - now once per second.
        // The gas also stops hurting the moment the last fuse is in (the box still counts down 4 s before the fans start).
        while (isTrapActive && timer > 0 && !FusesDone)
        {
            float dt = Time.deltaTime;
            timer -= dt;

            if (playerStats != null && !playerStats.IsDead() && dt > 0f)
            {
                // Force heavy breathing right away
                playerStats.TriggerHeavyBreathing(2f);

                // Start doing damage when less than half time remaining
                if (timer < (deathTimer / 2f))
                {
                    tick += dt;
                    if (tick >= 1f)
                    {
                        tick -= 1f;
                        playerStats.TakeDamage(damagePerSecond);
                    }
                }
            }

            yield return null;
        }

        // If time runs out and trap is still active
        if (isTrapActive && timer <= 0f && !FusesDone && playerStats != null)
        {
            playerStats.TakeDamage(9999f); // Instant kill
        }
    }

    private IEnumerator HallucinationRoutine()
    {
        // Don't hallucinate immediately, give them 15 seconds to panic first
        yield return new WaitForSeconds(15f);

        while (isTrapActive && mainCam != null && !FusesDone && (playerStats == null || !playerStats.IsDead()))
        {
            // Random wait between hallucinations
            yield return new WaitForSeconds(Random.Range(5f, 12f));

            if (!isTrapActive || FusesDone || (playerStats != null && playerStats.IsDead())) break;

            SpawnHallucination();
        }
    }

    private int hallucinationCount;

    /// <summary>
    /// The gas makes the player see the clown - the REAL animated clown model (not a stiff puppet):
    /// the first time he lunges out of the fog at the player's face; after that he may also be standing right beside
    /// the player (gone the moment he is looked at) or far off in the gas, staring.
    /// </summary>
    private void SpawnHallucination()
    {
        if (mainCam == null) return;
        JokerPhantom.Kind kind = hallucinationCount == 0 ? JokerPhantom.Kind.Lunge
            : (JokerPhantom.Kind)Random.Range(0, 3);
        hallucinationCount++;
        if (JokerPhantom.Run(this, mainCam, kind, fakeJokerPrefab, hallucinationSound)) return;
        if (kind != JokerPhantom.Kind.Behind && JokerPhantom.Run(this, mainCam, JokerPhantom.Kind.Behind, fakeJokerPrefab, hallucinationSound)) return;

        // no room in front of the player: just the sound
        if (hallucinationSound != null && myAudioSource != null) myAudioSource.PlayOneShot(hallucinationSound);
    }
}
