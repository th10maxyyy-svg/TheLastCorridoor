using UnityEngine;

public class PendulumSwing : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Swing Settings")]
    [Tooltip("Which direction to swing (Z is usually left-to-right)")]
    public Axis swingAxis = Axis.Z;

    [Tooltip("How far the bulb swings left and right (degrees)")]
    public float swingAngle = 15f;
    
    [Tooltip("How fast the bulb swings")]
    public float swingSpeed = 2f;
    
    [Tooltip("Add organic randomness so it doesn't look like a perfect clock")]
    public bool organicWind = true;

    private float timer = 0f;
    private Quaternion startRotation;
    private float windNoise = 0f;

    void Start()
    {
        startRotation = transform.localRotation;
    }

    void Update()
    {
        timer += Time.deltaTime * swingSpeed;

        // Calculate a basic pendulum sine wave
        float angle = Mathf.Sin(timer) * swingAngle;

        // Add organic wind variation using Perlin noise
        if (organicWind)
        {
            windNoise = (Mathf.PerlinNoise(Time.time * 0.5f, 0) - 0.5f) * 10f; // Random gust of wind
            angle += windNoise;
        }

        // Apply rotation based on the chosen axis
        Vector3 rotOffset = Vector3.zero;
        if (swingAxis == Axis.X) rotOffset = new Vector3(angle, 0, 0);
        else if (swingAxis == Axis.Y) rotOffset = new Vector3(0, angle, 0);
        else if (swingAxis == Axis.Z) rotOffset = new Vector3(0, 0, angle);

        transform.localRotation = startRotation * Quaternion.Euler(rotOffset);
    }
}
