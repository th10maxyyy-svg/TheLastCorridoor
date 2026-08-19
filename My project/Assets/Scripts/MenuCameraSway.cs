using UnityEngine;

public class MenuCameraSway : MonoBehaviour
{
    [Header("Cinematic Intro")]
    public bool playCinematicIntro = true;
    [Tooltip("Where the camera starts flying from")]
    public Transform introStartPoint;
    public float introDuration = 4.0f;
    [Tooltip("The Canvas to fade in after the camera lands")]
    public CanvasGroup uiCanvasGroup;

    private bool isPlayingIntro = false;

    [Header("Audio Settings")]
    [Tooltip("Audio source for one-time sound like pigeons flying")]
    public AudioSource sfxSource;
    [Tooltip("Audio source for the continuous menu theme")]
    public AudioSource musicSource;
    public AudioClip introSound;
    public AudioClip loopingTheme;

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

        if (playCinematicIntro && introStartPoint != null)
        {
            StartCoroutine(IntroSequence());
        }
        else if (uiCanvasGroup != null)
        {
            uiCanvasGroup.alpha = 1f;
        }
    }

    private System.Collections.IEnumerator IntroSequence()
    {
        isPlayingIntro = true;
        if (uiCanvasGroup != null) uiCanvasGroup.alpha = 0f;

        // Play Intro Sound (e.g. Pigeons flying)
        if (sfxSource != null && introSound != null)
        {
            sfxSource.PlayOneShot(introSound);
        }

        Vector3 initialPos = introStartPoint.localPosition;
        Quaternion initialRot = introStartPoint.localRotation;

        // Snap camera to start
        transform.localPosition = initialPos;
        transform.localRotation = initialRot;

        float elapsed = 0f;
        while (elapsed < introDuration)
        {
            float t = elapsed / introDuration;
            
            // Cubic Ease-Out: Tezi se start hoga aur end par ahista ahista rukega (Zyada cinematic lagta hai)
            t = 1f - Mathf.Pow(1f - t, 3f);

            // Calculate Base Position & Rotation
            Vector3 basePos = Vector3.Lerp(initialPos, startPosition, t);
            Quaternion baseRot = Quaternion.Slerp(initialRot, startRotation, t);
            
            // --- NATURAL CHAOTIC MOVEMENT ---
            // 'introIntensity' shuru mein full hoga (1) aur end par 0 ho jayega
            float introIntensity = 1f - t; 
            
            // Normal saans lene wala sway
            float noiseX = (Mathf.PerlinNoise(Time.time * swaySpeed + offsetX, 0) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0, Time.time * swaySpeed + offsetY) - 0.5f) * 2f;

            // Tehra-Mera (Crooked) aur Up/Down Left/Right bhari movement
            float chaosX = (Mathf.PerlinNoise(Time.time * 3f + offsetX, 10) - 0.5f) * 2.5f * introIntensity;
            float chaosY = (Mathf.PerlinNoise(10, Time.time * 3f + offsetY) - 0.5f) * 2.5f * introIntensity;
            
            // Camera ka tilt (Z-axis par roll) taake bilkul natural/drunken lagay
            float tiltZ = (Mathf.PerlinNoise(Time.time * 2.5f, Time.time * 2.5f) - 0.5f) * 15f * introIntensity;

            Vector3 finalPos = basePos + new Vector3(noiseX * swayAmount + chaosX, noiseY * swayAmount + chaosY, 0);
            
            // Rotation mein extra chaos aur tilt daal diya
            Quaternion finalRot = baseRot * Quaternion.Euler(
                -noiseY * rotationAmount - chaosY * 3f, // Up/Down 
                noiseX * rotationAmount + chaosX * 3f,  // Left/Right
                tiltZ                                   // Roll (Tehra hona)
            );

            transform.localPosition = finalPos;
            transform.localRotation = finalRot;

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = startPosition;
        transform.localRotation = startRotation;

        // Fade in UI aur Music
        if (musicSource != null && loopingTheme != null)
        {
            musicSource.clip = loopingTheme;
            musicSource.loop = true;
            musicSource.volume = 0f;
            musicSource.Play();
        }

        if (uiCanvasGroup != null || musicSource != null)
        {
            float fadeElapsed = 0f;
            while (fadeElapsed < 1.5f)
            {
                float fadeValue = fadeElapsed / 1.5f;
                
                if (uiCanvasGroup != null)
                    uiCanvasGroup.alpha = Mathf.Lerp(0f, 1f, fadeValue);
                    
                if (musicSource != null)
                    musicSource.volume = Mathf.Lerp(0f, 1f, fadeValue);
                    
                fadeElapsed += Time.deltaTime;
                yield return null;
            }
            if (uiCanvasGroup != null) uiCanvasGroup.alpha = 1f;
            if (musicSource != null) musicSource.volume = 1f;
        }

        isPlayingIntro = false;
    }

    void Update()
    {
        if (isPlayingIntro) return; // Don't do normal sway while flying in

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
