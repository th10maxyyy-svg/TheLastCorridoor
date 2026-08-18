using UnityEngine;

public class MenuCameraSway : MonoBehaviour
{
    [Header("Sway Settings")]
    [Tooltip("How much the camera breathes/sways (Position)")]
    public float swayAmount = 0.05f;
    [Tooltip("How much the camera rotates (Rotation)")]
    public float rotationAmount = 1.0f;
    [Tooltip("How fast the camera moves")]
    public float swaySpeed = 1.0f;

    [Header("Mouse Look (Optional)")]
    [Tooltip("Check this if you want the camera to slightly follow the mouse pointer")]
    public bool followMouse = true;
    public float mouseFollowSpeed = 2f;
    public float maxMouseAngle = 5f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    
    // For random noise generation
    private float offsetX;
    private float offsetY;

    void Start()
    {
        startPosition = transform.localPosition;
        startRotation = transform.localRotation;
        
        // Randomize the start so it feels organic
        offsetX = Random.Range(0f, 1000f);
        offsetY = Random.Range(0f, 1000f);
    }

    void Update()
    {
        // Calculate organic breathing using Perlin Noise
        float noiseX = (Mathf.PerlinNoise(Time.time * swaySpeed + offsetX, 0) - 0.5f) * 2f;
        float noiseY = (Mathf.PerlinNoise(0, Time.time * swaySpeed + offsetY) - 0.5f) * 2f;

        Vector3 targetPosition = startPosition + new Vector3(noiseX * swayAmount, noiseY * swayAmount, 0);
        
        // Base rotational sway
        Vector3 rotOffset = new Vector3(-noiseY * rotationAmount, noiseX * rotationAmount, noiseX * (rotationAmount * 0.5f));
        
        // Mouse follow logic
        if (followMouse)
        {
            // Get mouse position relative to center of screen (-1 to 1)
            float mouseX = (Input.mousePosition.x / Screen.width) * 2f - 1f;
            float mouseY = (Input.mousePosition.y / Screen.height) * 2f - 1f;
            
            // Clamp just in case
            mouseX = Mathf.Clamp(mouseX, -1f, 1f);
            mouseY = Mathf.Clamp(mouseY, -1f, 1f);
            
            // Add mouse rotation to our sway
            rotOffset += new Vector3(-mouseY * maxMouseAngle, mouseX * maxMouseAngle, 0);
        }

        // Apply Position and Rotation smoothly
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * swaySpeed);
        
        Quaternion targetRotation = startRotation * Quaternion.Euler(rotOffset);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * (followMouse ? mouseFollowSpeed : swaySpeed));
    }
}
