using UnityEngine;

public class LightFlicker : MonoBehaviour
{
    [Header("Flicker Settings")]
    public Light targetLight;
    public float minIntensity = 0f;
    public float maxIntensity = 2f;
    
    [Tooltip("How fast the light flickers (lower is faster)")]
    public float flickerSpeed = 0.1f;

    [Tooltip("Chance to completely turn off for a split second (0 to 1)")]
    public float dropOutChance = 0.1f;

    private float timer;
    private float baseIntensity;

    void Start()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light>();

        if (targetLight != null)
            baseIntensity = targetLight.intensity;
    }

    void Update()
    {
        if (targetLight == null) return;

        timer -= Time.deltaTime;

        if (timer <= 0)
        {
            // Randomly decide if the light drops out completely
            if (Random.value < dropOutChance)
            {
                targetLight.intensity = minIntensity;
                timer = Random.Range(0.05f, 0.2f); // Stay off for a short time
            }
            else
            {
                // Normal flicker
                targetLight.intensity = Random.Range(baseIntensity * 0.5f, maxIntensity);
                timer = Random.Range(0.02f, flickerSpeed);
            }
        }
    }
}
