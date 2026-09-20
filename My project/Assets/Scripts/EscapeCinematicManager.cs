using UnityEngine;
using System.Collections;
using TMPro;

public class EscapeCinematicManager : MonoBehaviour
{
    [Header("Cinematic Elements")]
    public Camera cinematicCamera; // The camera placed outside looking at the door
    public CanvasGroup fadeCanvasGroup; // The black screen canvas group
    public TextMeshProUGUI endingText; // The text to display at the end

    [Header("Camera Movement & Shake")]
    public Transform cameraStartPoint; // Start near the door
    public Transform cameraEndPoint;   // Pull back away from the door
    public float cameraMoveSpeed = 1f;
    public float cameraShakeAmount = 0.05f; // Intensity of fear shake
    public float cameraShakeSpeed = 20f; // Speed of the shake

    [Header("Enemy Control")]
    public EnemyAI enemyAI;
    public Transform enemyTeleportPoint; // Where the enemy starts the chase (inside the hall)
    public Transform enemyDoorPoint; // The edge of the light where he screams
    public Transform enemyRetreatPoint; // Where he walks back into the darkness
    public string enemyAnimationTriggerName = "Tease"; // Let the user type their new animation name!

    [Header("Settings")]
    public string finalMessage = "The Nightmare is over... for now.";
    public float fadeDuration = 3f;
    public float timeBeforeFade = 6f; // How long to watch the enemy retreat before fading

    private bool hasTriggered = false;
    private bool isCameraMoving = false;

    private void Start()
    {
        if (cinematicCamera != null) cinematicCamera.gameObject.SetActive(false);
        if (endingText != null) endingText.text = "";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!hasTriggered && other.CompareTag("Player"))
        {
            hasTriggered = true;
            StartCoroutine(PlayCinematicSequence(other.gameObject));
        }
    }

    private Vector3 currentBasePosition;

    private void Update()
    {
        // Handle Camera Pan and Shake during cinematic
        if (isCameraMoving && cinematicCamera != null)
        {
            // Move our internal base position BACKWARDS from the door automatically
            float step = cameraMoveSpeed * Time.deltaTime;
            currentBasePosition += transform.forward * step; // Assuming Z is forward, this pulls it back if trigger is facing out. If it goes wrong way, we use Z axis directly.

            
            // Calculate Fear Shake Effect using Perlin Noise
            float offsetX = (Mathf.PerlinNoise(Time.time * cameraShakeSpeed, 0) - 0.5f) * cameraShakeAmount;
            float offsetY = (Mathf.PerlinNoise(0, Time.time * cameraShakeSpeed) - 0.5f) * cameraShakeAmount;
            
            // Apply base position + shake offset directly to camera
            cinematicCamera.transform.position = currentBasePosition + new Vector3(offsetX, offsetY, 0);
            
            // ALWAYS LOOK AT THE ENEMY!
            if (enemyAI != null)
            {
                // Look slightly up at the enemy's chest/face instead of their feet
                Vector3 targetLookPos = enemyAI.transform.position + Vector3.up * 1.5f; 
                cinematicCamera.transform.LookAt(targetLookPos);
            }
            else
            {
                // Fallback if enemy is missing
                cinematicCamera.transform.rotation = Quaternion.Slerp(cinematicCamera.transform.rotation, cameraEndPoint.rotation, step);
            }
        }
    }

    private IEnumerator PlayCinematicSequence(GameObject player)
    {
        Debug.Log("Starting Grand Escape Cinematic!");

        // 1. Freeze Player, hide their body, and switch to Cinematic Camera
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.enabled = false;

        InteractionRaycast ir = Camera.main.GetComponent<InteractionRaycast>();
        if (ir != null) ir.enabled = false;

        // Hide the player's body so they don't block the camera view
        Renderer[] playerRenderers = player.GetComponentsInChildren<Renderer>();
        foreach(Renderer r in playerRenderers) { r.enabled = false; }

        // Setup Camera Position Automatically! (No need for Start/End points)
        // We use the Trigger's position (the door) and place the camera just outside it.
        Vector3 startPos = transform.position + new Vector3(0, 1.5f, 1f); // Adjust height and push out a bit
        
        if (cinematicCamera != null)
        {
            cinematicCamera.transform.position = startPos;
            currentBasePosition = startPos; // NEW: Initialize base position for shake
        }

        // Turn off player's camera and turn on cinematic camera
        Camera.main.gameObject.SetActive(false);
        if (cinematicCamera != null) cinematicCamera.gameObject.SetActive(true);
        isCameraMoving = true;

        // Turn off player's UI completely (to remove blood splatters/health bars)
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas c in allCanvases)
        {
            // Don't disable the canvas that has our fade screen!
            if (fadeCanvasGroup != null && !c.transform.IsChildOf(fadeCanvasGroup.transform) && c.transform != fadeCanvasGroup.transform)
            {
                // If it's the main gameplay canvas, disable it
                if (c.name == "Canvas" || c.name.Contains("UI"))
                {
                    c.gameObject.SetActive(false);
                }
            }
        }

        // Start Escape Phase (Progression Manager)
        ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
        if (pm != null) pm.StartEscapePhase();

        // 2. Teleport the Enemy and make him run towards the door
        if (enemyAI != null && enemyTeleportPoint != null)
        {
            enemyAI.gameObject.SetActive(true); // Ensure he is active
            
            UnityEngine.AI.NavMeshAgent agent = enemyAI.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(enemyTeleportPoint.position, out hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
                else
                {
                    agent.Warp(enemyTeleportPoint.position); // Fallback
                }
            }
            
            // Force the enemy to play the cinematic escape behavior with custom animation
            enemyAI.PlayCinematicEscape(enemyDoorPoint, enemyRetreatPoint, enemyAnimationTriggerName);
        }

        // 3. Wait for the enemy to run, scream and start retreating
        yield return new WaitForSeconds(timeBeforeFade);

        isCameraMoving = false; // Stop the camera shake/pan when fading

        // 4. Fade to Black
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        // 5. Show Ending Text
        if (endingText != null)
        {
            endingText.text = finalMessage;
            yield return new WaitForSeconds(4f); // Let the player read it
        }

        // 6. Delete Save File (Permadeath/Game Complete) and Load Main Menu
        if (SaveManager.instance != null)
        {
            SaveManager.instance.DeleteSave();
        }

        // Return cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UnityEngine.SceneManagement.SceneManager.LoadScene(0); // Load Main Menu
    }
}
