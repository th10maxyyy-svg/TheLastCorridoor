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
    public GameObject gasParticleSystem; // The green fog/gas visual
    public AudioSource gasHissingSound;
    public AudioSource hallucinationSound; // Random spooky noises

    [Header("Hallucination Settings")]
    public GameObject fakeJokerPrefab;
    [Tooltip("How close the fake Joker spawns to the player")]
    public float spawnDistance = 2.5f;

    private bool isTrapActive = false;
    private PlayerStats playerStats;
    private Camera mainCam;

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

        // Lock the door behind them
        if (entranceDoor != null)
        {
            var type = entranceDoor.GetType();
            
            // Force it closed
            var isOpenField = type.GetField("isOpen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (isOpenField != null) isOpenField.SetValue(entranceDoor, false);

            // Force it locked
            var isLockedField = type.GetField("isLocked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (isLockedField != null) isLockedField.SetValue(entranceDoor, true);

            // Set required key to something impossible so they can't open it normally
            var keyField = type.GetField("requiredKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (keyField != null) keyField.SetValue(entranceDoor, "TRAP_LOCKED_KEY");

            // Play the closing sound manually if possible
            var audioSourceField = type.GetField("audioSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var closeSoundField = type.GetField("closeSound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (audioSourceField != null && closeSoundField != null)
            {
                AudioSource src = (AudioSource)audioSourceField.GetValue(entranceDoor);
                AudioClip clip = (AudioClip)closeSoundField.GetValue(entranceDoor);
                if (src != null && clip != null) src.PlayOneShot(clip);
            }

            Debug.Log("ToxicGasTrap: Entrance Door SLAMMED and LOCKED!");
        }

        // Start visuals and audio
        if (gasParticleSystem != null) gasParticleSystem.SetActive(true);
        if (gasHissingSound != null) gasHissingSound.Play();

        // Start the death sequence
        StartCoroutine(GasTrapRoutine());
        StartCoroutine(HallucinationRoutine());
        
        // Hide the trigger so it doesn't trigger twice
        GetComponent<Collider>().enabled = false;
    }

    public void StopTrap()
    {
        if (!isTrapActive) return;
        isTrapActive = false;
        
        Debug.Log("ToxicGasTrap: Gas stopped! Valve turned.");

        if (gasParticleSystem != null) gasParticleSystem.SetActive(false);
        if (gasHissingSound != null) gasHissingSound.Stop();

        // If you want to unlock a door here, you can!
    }

    private IEnumerator GasTrapRoutine()
    {
        float timer = deathTimer;

        while (isTrapActive && timer > 0)
        {
            timer -= Time.deltaTime;

            if (playerStats != null)
            {
                // Force heavy breathing right away
                playerStats.TriggerHeavyBreathing(2f);

                // Start doing damage when less than half time remaining
                if (timer < (deathTimer / 2f))
                {
                    playerStats.TakeDamage(damagePerSecond * Time.deltaTime);
                }
            }

            yield return null;
        }

        // If time runs out and trap is still active
        if (isTrapActive && playerStats != null)
        {
            playerStats.TakeDamage(9999f); // Instant kill
        }
    }

    private IEnumerator HallucinationRoutine()
    {
        // Don't hallucinate immediately, give them 15 seconds to panic first
        yield return new WaitForSeconds(15f);

        while (isTrapActive && mainCam != null)
        {
            // Random wait between hallucinations
            yield return new WaitForSeconds(Random.Range(5f, 12f));

            if (!isTrapActive) break;

            SpawnHallucination();
        }
    }

    private void SpawnHallucination()
    {
        if (fakeJokerPrefab == null) return;

        // Spawn right in front of the camera, but slightly to the side or directly ahead
        Vector3 spawnPos = mainCam.transform.position + mainCam.transform.forward * spawnDistance;
        spawnPos.y = mainCam.transform.position.y - 1f; // Adjust height slightly down

        GameObject fakeJoker = Instantiate(fakeJokerPrefab, spawnPos, Quaternion.LookRotation(mainCam.transform.position - spawnPos));
        
        if (hallucinationSound != null)
        {
            hallucinationSound.Play();
        }

        // Destroy it after a split second
        Destroy(fakeJoker, 0.4f);
    }
}
