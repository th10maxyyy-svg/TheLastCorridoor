using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ProceduralSparkSound : MonoBehaviour
{
    private double sampleRate;
    private double phaseHum = 0;
    private double phaseSpark = 0;
    private System.Random rand = new System.Random();
    
    public bool isEnabled = true;
    
    private Transform listener;
    private float currentVolumeMultiplier = 1f;
    
    [Header("3D Sound Settings")]
    [Tooltip("The distance where the sound completely fades out.")]
    [SerializeField] private float maxDist = 4.5f; 
    
    [Tooltip("The distance where the sound is at max volume.")]
    [SerializeField] private float minDist = 0.5f;

    void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;
        AudioSource src = GetComponent<AudioSource>();
        src.spatialBlend = 1f; // Make it 3D sound so it fades when you walk away
        src.loop = true;
        src.volume = 0.7f;
        
        // Restrict sound to the room only
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = minDist;  
        src.maxDistance = maxDist; 
        
        // Create a dummy clip so the AudioSource plays and triggers our procedural audio
        src.clip = AudioClip.Create("DummySpark", (int)sampleRate, 1, (int)sampleRate, false);
        src.Play();
        
        AudioListener al = FindAnyObjectByType<AudioListener>();
        if (al != null) listener = al.transform;
    }

    void Update()
    {
        // Calculate distance on the main thread (Unity API is not thread-safe for audio thread)
        if (listener != null)
        {
            float dist = Vector3.Distance(transform.position, listener.position);
            if (dist > maxDist) 
                currentVolumeMultiplier = 0f;
            else if (dist <= minDist) 
                currentVolumeMultiplier = 1f;
            else 
                currentVolumeMultiplier = 1f - ((dist - minDist) / (maxDist - minDist));
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!isEnabled)
        {
            // Output silence when disabled
            for (int i = 0; i < data.Length; i++) data[i] = 0;
            return;
        }

        for (int i = 0; i < data.Length; i += channels)
        {
            // 60 Hz Electric Hum (Mains electricity sound)
            phaseHum += 60.0 / sampleRate;
            if (phaseHum > 1.0) phaseHum -= 1.0;
            float hum = Mathf.Sin((float)(phaseHum * 2 * Mathf.PI)) * 0.15f;
            
            // 120 Hz Buzz (Harmonics of the hum)
            float buzz = Mathf.Sin((float)(phaseHum * 4 * Mathf.PI)) * 0.05f;

            // Sparks (Loud bursts of noise)
            float noise = (float)(rand.NextDouble() * 2.0 - 1.0);
            
            // Random chance for a loud spark burst to happen
            float sparkEnvelope = 0f;
            if (rand.NextDouble() < 0.001) // Adjust this for more/less sparks
            {
                phaseSpark = 1.0; // Max spark energy
            }
            
            if (phaseSpark > 0)
            {
                sparkEnvelope = (float)phaseSpark;
                phaseSpark -= 0.008; // How fast the spark fades out
            }

            float spark = noise * sparkEnvelope * 0.6f;

            // Combine and apply manual 3D distance volume
            float sample = (hum + buzz + spark) * currentVolumeMultiplier;

            for (int c = 0; c < channels; c++)
            {
                data[i + c] = sample;
            }
        }
    }
}
