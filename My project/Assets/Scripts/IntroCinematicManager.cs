using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class IntroCinematicManager : MonoBehaviour
{
    [Header("Player References")]
    public PlayerController playerController;
    public PlayerStats playerStats; // ADDED: For breathing
    public Camera playerCamera;
    public GameObject uiCanvas; // Reference to the canvas holding Health/Stamina

    [Header("Cinematic Settings")]
    public Image blinkOverlay; // A black UI image that covers the screen
    public float restrictedLookSpeed = 0.5f; // Slower speed
    public float lookSmoothness = 2f; // Sluggish feel
    public float maxLookAngleH = 30f;
    public float maxLookAngleV = 15f;

    private bool isCinematicActive = false;
    private float currentRotX = 0f;
    private float currentRotY = 0f;
    private Quaternion startCamRot;
    private Vector3 startCamPos;
    private Coroutine blinkRoutine;
    private bool endRequested = false;

    void Start()
    {
        // If the player died and the scene was reloaded, skip the intro entirely
        if (IntroState.skipIntro)
        {
            if (blinkOverlay != null) SetAlpha(blinkOverlay, 0f);

            if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
            {
                playerController.isCinematicPlaying = false;
                playerController.enabled = true;
            }
            if (uiCanvas != null) uiCanvas.SetActive(true);

            Destroy(gameObject);
            return;
        }

        // Try auto-assigning if left empty
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        if (playerStats == null && playerController != null) playerStats = playerController.GetComponent<PlayerStats>();
        if (playerCamera == null && playerController != null) playerCamera = playerController.GetComponentInChildren<Camera>();

        // Start the cinematic immediately when the scene loads
        blinkRoutine = StartCoroutine(StartCinematicRoutine());
    }

    IEnumerator StartCinematicRoutine()
    {
        // BUG FIX: Wait 1 frame to ensure PlayerController's Start() method runs first!
        // Otherwise playerCamera remains null in PlayerController!
        yield return null;

        // The intro is now running. From here on, ANY reload of this scene in the same session
        // (death respawn, restart, etc.) must skip the intro + projector video.
        // Set a frame later so every Start() of this load has already read the flag.
        IntroState.skipIntro = true;

        isCinematicActive = true;
        
        // Disable regular player movement and hide UI
        if (playerController != null)
        {
            playerController.isCinematicPlaying = true;
            playerController.enabled = false;
        }
        if (uiCanvas != null) uiCanvas.SetActive(false);

        if (playerCamera != null)
        {
            startCamRot = playerCamera.transform.localRotation;
            startCamPos = playerCamera.transform.localPosition;
        }

        // Video already finished (empty playlist / skipped) before we got here - end right away instead of waiting forever
        if (endRequested)
        {
            StartCoroutine(FaintAndWakeUpRoutine());
            yield break;
        }
        
        // --- REALISTIC WAKE UP BLINKING ---
        SetAlpha(blinkOverlay, 1f); // Completely closed
        yield return new WaitForSeconds(1.5f);

        // Blink 1: Tries to open, but fails (opens halfway)
        yield return StartCoroutine(FadeImage(blinkOverlay, 1f, 0.5f, 1.2f));
        yield return StartCoroutine(FadeImage(blinkOverlay, 0.5f, 1f, 0.3f));
        yield return new WaitForSeconds(0.3f);

        // Blink 2: Opens a bit more, blurry
        yield return StartCoroutine(FadeImage(blinkOverlay, 1f, 0.2f, 0.8f));
        yield return StartCoroutine(FadeImage(blinkOverlay, 0.2f, 1f, 0.2f));
        yield return new WaitForSeconds(0.1f);

        // Trigger heavy breathing right as they force their eyes open
        if (playerStats != null) playerStats.TriggerHeavyBreathing(20f);

        // Blink 3: Forces eyes wide open
        yield return StartCoroutine(FadeImage(blinkOverlay, 1f, 0f, 2f));
        
        // Watch the video for a few seconds
        yield return new WaitForSeconds(3.5f);

        // Random sharp blink while watching
        yield return StartCoroutine(FadeImage(blinkOverlay, 0f, 1f, 0.1f));
        yield return StartCoroutine(FadeImage(blinkOverlay, 1f, 0f, 0.2f));

        yield return new WaitForSeconds(4f);

        // Another random sharp blink (struggling to stay awake)
        yield return StartCoroutine(FadeImage(blinkOverlay, 0f, 1f, 0.15f));
        yield return StartCoroutine(FadeImage(blinkOverlay, 1f, 0f, 0.3f));

        // Now the player is just watching the videos until they finish... 
    }

    void Update()
    {
        if (isCinematicActive && playerCamera != null)
        {
            // Restricted look logic
            float mouseX = Input.GetAxis("Mouse X") * restrictedLookSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * restrictedLookSpeed;

            currentRotY += mouseX;
            currentRotX -= mouseY;

            // Clamp angles so they can only look slightly around
            currentRotY = Mathf.Clamp(currentRotY, -maxLookAngleH, maxLookAngleH);
            currentRotX = Mathf.Clamp(currentRotX, -maxLookAngleV, maxLookAngleV);

            // Apply sluggish (lerped) rotation
            Quaternion targetRot = startCamRot * Quaternion.Euler(currentRotX, currentRotY, 0);
            playerCamera.transform.localRotation = Quaternion.Slerp(playerCamera.transform.localRotation, targetRot, Time.deltaTime * lookSmoothness);
        }
    }

    // Call this from the VideoPlaylist's OnPlaylistFinished event!
    public void EndCinematicAndFaint()
    {
        if (endRequested) return;
        endRequested = true;

        if (isCinematicActive)
        {
            // Stop the blinking routine so two coroutines don't fight over the black overlay
            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            StartCoroutine(FaintAndWakeUpRoutine());
        }
    }

    IEnumerator FaintAndWakeUpRoutine()
    {
        isCinematicActive = false; // Stop the sluggish mouse look

        // 1. FALL TO THE GROUND ANIMATION (Girnay wali animation)
        if (playerCamera != null)
        {
            float fallDuration = 1.5f;
            float elapsed = 0f;
            Vector3 currentPos = playerCamera.transform.localPosition;
            Quaternion currentRot = playerCamera.transform.localRotation;

            Vector3 targetFallPos = new Vector3(0f, 0.2f, 0f); // Ground level
            Quaternion targetFallRot = Quaternion.Euler(0f, 0f, 75f); // Tilted head

            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / fallDuration);
                playerCamera.transform.localPosition = Vector3.Lerp(currentPos, targetFallPos, t);
                playerCamera.transform.localRotation = Quaternion.Slerp(currentRot, targetFallRot, t);
                yield return null;
            }
        }

        // 2. Faint (Eyes close slowly to black)
        yield return StartCoroutine(FadeImage(blinkOverlay, 0f, 1f, 2f));
        
        // Wait while unconscious in the dark
        yield return new WaitForSeconds(2f);
        
        // Reset camera perfectly so WakeUpSequence can take over cleanly
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = startCamRot; 
        }

        // Now trigger the original Get-Up-From-Floor cinematic!
        if (uiCanvas != null) uiCanvas.SetActive(true); // Re-enable health/stamina bars

        if (playerController != null)
        {
            playerController.enabled = true; // Make sure script is on
            playerController.isCinematicPlaying = false; // Release the strict lock
            playerController.TriggerWakeUpSequence(); // Start the floor get-up
        }

        // Hide our blink overlay completely since the PlayerController's fadeImage takes over
        SetAlpha(blinkOverlay, 0f);
        
        // Destroy this manager, its job is done
        Destroy(gameObject);
    }

    // Helper coroutine to fade UI images
    IEnumerator FadeImage(Image img, float startAlpha, float endAlpha, float duration)
    {
        if (img == null) yield break;

        float elapsed = 0f;
        Color c = img.color;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            img.color = c;
            yield return null;
        }
        c.a = endAlpha;
        img.color = c;
    }

    void SetAlpha(Image img, float alpha)
    {
        if (img != null)
        {
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }
}
