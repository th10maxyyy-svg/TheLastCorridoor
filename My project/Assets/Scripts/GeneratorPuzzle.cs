using UnityEngine;
using UnityEngine.Events;

public class GeneratorPuzzle : MonoBehaviour, IInteractable
{
    [Header("Generator Settings")]
    public string requiredItem = "Crank Handle";
    public bool isGeneratorOn = false;

    [Header("Audio & Visuals")]
    public AudioSource audioSource;
    public AudioClip startupSound;
    public AudioClip runningLoopSound;
    
    [Tooltip("Lights that should turn on when the generator starts")]
    public GameObject[] generatorLights;

    [Header("Events")]
    public UnityEvent onGeneratorStarted;

    [Tooltip("After starting, the engine is heard for this many seconds, then it fades out (it was noisy in the whole level)")]
    public float runningSeconds = 5f;

    private System.Collections.IEnumerator QuietAfterStart(float wait)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, wait));
        if (audioSource == null) yield break;
        float v0 = audioSource.volume;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.5f)
        {
            audioSource.volume = v0 * (1f - t);
            yield return null;
        }
        audioSource.Stop();
        audioSource.loop = false;
        audioSource.clip = null;
        audioSource.volume = v0;
    }

    private string SaveFlag => "GenStarted_" + SaveManager.GetSceneObjectId(transform);

    void Start()
    {
        EnsureAudio();
        if (SaveManager.instance != null && SaveManager.instance.HasFlag(SaveFlag))
        {
            isGeneratorOn = true;   // already started earlier: it stays quiet (no engine noise all over the level)
            if (generatorLights != null)
            {
                foreach (GameObject light in generatorLights)
                {
                    if (light != null) light.SetActive(true);
                }
            }
            onGeneratorStarted?.Invoke();
        }
    }

    /// <summary>
    /// BUG FIX: the basement generator had no AudioSource / sounds assigned, so it started in silence.
    /// Falls back to the built-in engine sounds (Resources/HorrorAudio) and a 3D source on the generator.
    /// </summary>
    private void EnsureAudio()
    {
        if (startupSound == null) startupSound = Resources.Load<AudioClip>("HorrorAudio/TLC_Generator_Start");
        if (runningLoopSound == null) runningLoopSound = Resources.Load<AudioClip>("HorrorAudio/TLC_Generator_Loop");
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 3f;
            audioSource.maxDistance = 28f;
            audioSource.volume = 0.85f;
            audioSource.dopplerLevel = 0f;
        }
    }

    public string GetInteractPrompt()
    {
        if (isGeneratorOn) return "Generator is running smoothly.";
        return "Press E to start Generator (Needs " + requiredItem + ")";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isGeneratorOn) return;

        Inventory inventory = playerStats.GetComponent<Inventory>();
        if (inventory != null && inventory.HasItem(requiredItem))
        {
            inventory.RemoveItem(requiredItem, false);   // USED UP: the handle stays in the generator, it can not be used again
            StartGenerator();
            
            // Show notification
            InteractionRaycast.Notify(playerStats, "Power Restored!");
        }
        else
        {
            InteractionRaycast.Notify(playerStats, "You need a " + requiredItem + " to start this.");
        }
    }

    private void StartGenerator()
    {
        isGeneratorOn = true;
        if (SaveManager.instance != null) SaveManager.instance.SetFlag(SaveFlag);

        if (audioSource != null && startupSound != null)
        {
            audioSource.PlayOneShot(startupSound);
            // Wait for startup to finish then play loop
            if (runningLoopSound != null)
            {
                audioSource.clip = runningLoopSound;
                audioSource.loop = true;
                audioSource.PlayDelayed(startupSound.length);
            }
            StartCoroutine(QuietAfterStart(startupSound.length + runningSeconds));
        }

        if (generatorLights != null)
        foreach (GameObject light in generatorLights)
        {
            if (light != null) light.SetActive(true);
        }

        onGeneratorStarted?.Invoke();
    }
}
