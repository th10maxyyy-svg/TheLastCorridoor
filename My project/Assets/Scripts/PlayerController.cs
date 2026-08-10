using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 4.0f;
    [SerializeField] private float sprintSpeed = 7.0f;
    [SerializeField] private float crouchSpeed = 2.0f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float jumpHeight = 1.2f;

    [Header("Crouch Settings")]
    [SerializeField] private float standingHeight = 2.0f;
    [SerializeField] private float crouchingHeight = 1.0f;
    [SerializeField] private float timeToCrouch = 0.2f;
    [SerializeField] private Vector3 standingCameraPosition = new Vector3(0, 0.8f, 0);
    [SerializeField] private Vector3 crouchingCameraPosition = new Vector3(0, 0.3f, 0);

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float upperLookLimit = -80.0f;
    [SerializeField] private float lowerLookLimit = 80.0f;

    [Header("Headbob & Footsteps")]
    [SerializeField] private float walkBobSpeed = 14f;
    [SerializeField] private float walkBobAmount = 0.05f;
    [SerializeField] private float sprintBobSpeed = 18f;
    [SerializeField] private float sprintBobAmount = 0.1f;
    [SerializeField] private float crouchBobSpeed = 8f;
    [SerializeField] private float crouchBobAmount = 0.02f;
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField] private float baseStepSpeed = 0.5f;
    [SerializeField] private float crouchStepMultiplier = 1.5f;
    [SerializeField] private float sprintStepMultiplier = 0.6f;

    [Header("Cinematic")]
    [SerializeField] private bool playWakeUpCinematic = false;
    [SerializeField] private UnityEngine.UI.Image fadeImage;

    private CharacterController characterController;
    private bool isWakingUp = false;
    private Camera playerCamera;
    private PlayerStats playerStats;

    private Vector3 moveDirection;
    private Vector2 currentInput;
    private float rotationX = 0;
    private float currentSpeed;
    private bool isCrouching;
    private bool isSprinting;
    
    // Headbob & Footstep variables
    private float defaultYPos = 0;
    private float timer = 0;
    private float footstepTimer = 0;
    
    public bool isHidden { get; private set; }
    private Transform currentExitPos;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        playerStats = GetComponent<PlayerStats>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentSpeed = walkSpeed;
        if (playerCamera != null)
        {
            defaultYPos = playerCamera.transform.localPosition.y;
        }

        if (playWakeUpCinematic)
        {
            StartCoroutine(WakeUpSequence());
        }
    }

    private float hideTime;

    void Update()
    {
        if (isWakingUp) return; // Paralyze player during cinematic

        if (isHidden)
        {
            if (Time.time > hideTime + 0.2f && Input.GetKeyDown(KeyCode.E))
            {
                ExitHiding();
            }
            HandleMouseLook(); // Allows looking around while hiding
            return;
        }

        HandleMovementInput();
        HandleMouseLook();
        HandleCrouch();
        ApplyMovement();
        HandleHeadBob();
        HandleFootsteps();
    }

    public void EnterHiding(Transform hidePos, Transform exitPos)
    {
        isHidden = true;
        hideTime = Time.time;
        currentExitPos = exitPos;
        characterController.enabled = false;
        transform.position = hidePos.position;
        // Keep camera rotation intact so player isn't disoriented
    }

    public void ExitHiding()
    {
        transform.position = currentExitPos.position;
        characterController.enabled = true;
        isHidden = false;
        currentExitPos = null;
    }

    private void HandleMovementInput()
    {
        float moveDirectionY = moveDirection.y;
        currentInput = new Vector2(Input.GetAxis("Vertical"), Input.GetAxis("Horizontal"));

        isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching && currentInput.x > 0;

        if (isCrouching)
        {
            currentSpeed = crouchSpeed;
        }
        else if (isSprinting && playerStats != null && playerStats.HasStamina())
        {
            currentSpeed = sprintSpeed;
            playerStats.UseStamina(Time.deltaTime);
        }
        else
        {
            currentSpeed = walkSpeed;
        }

        Vector3 moveForward = transform.TransformDirection(Vector3.forward) * currentInput.x;
        Vector3 moveRight = transform.TransformDirection(Vector3.right) * currentInput.y;
        
        moveDirection = (moveForward + moveRight).normalized * currentSpeed;
        moveDirection.y = moveDirectionY;
    }

    private void HandleMouseLook()
    {
        if (playerCamera == null) return;

        transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * mouseSensitivity);

        rotationX -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        rotationX = Mathf.Clamp(rotationX, upperLookLimit, lowerLookLimit);
        
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
    }

    private void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            isCrouching = !isCrouching;
            StartCoroutine(CrouchStandRoutine());
        }
    }

    private System.Collections.IEnumerator CrouchStandRoutine()
    {
        float targetHeight = isCrouching ? crouchingHeight : standingHeight;
        Vector3 targetCamPosition = isCrouching ? crouchingCameraPosition : standingCameraPosition;
        float currentHeight = characterController.height;
        Vector3 currentCamPos = playerCamera.transform.localPosition;

        float elapsedTime = 0f;

        while (elapsedTime < timeToCrouch)
        {
            characterController.height = Mathf.Lerp(currentHeight, targetHeight, elapsedTime / timeToCrouch);
            playerCamera.transform.localPosition = Vector3.Lerp(currentCamPos, targetCamPosition, elapsedTime / timeToCrouch);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        characterController.height = targetHeight;
        playerCamera.transform.localPosition = targetCamPosition;
        
        // Update defaultYPos for headbob base calculation
        defaultYPos = targetCamPosition.y;
    }

    private void ApplyMovement()
    {
        if (!characterController.isGrounded)
        {
            moveDirection.y += gravity * Time.deltaTime;
        }
        else
        {
            if (moveDirection.y < 0)
                moveDirection.y = -2.0f;

            if (Input.GetButtonDown("Jump"))
            {
                moveDirection.y = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
            }
        }

        characterController.Move(moveDirection * Time.deltaTime);
    }

    private void HandleHeadBob()
    {
        if (!characterController.isGrounded) return;

        if (Mathf.Abs(moveDirection.x) > 0.1f || Mathf.Abs(moveDirection.z) > 0.1f)
        {
            timer += Time.deltaTime * (isCrouching ? crouchBobSpeed : isSprinting ? sprintBobSpeed : walkBobSpeed);
            playerCamera.transform.localPosition = new Vector3(
                playerCamera.transform.localPosition.x,
                defaultYPos + Mathf.Sin(timer) * (isCrouching ? crouchBobAmount : isSprinting ? sprintBobAmount : walkBobAmount),
                playerCamera.transform.localPosition.z);
        }
        else
        {
            timer = 0;
            playerCamera.transform.localPosition = new Vector3(
                playerCamera.transform.localPosition.x,
                Mathf.Lerp(playerCamera.transform.localPosition.y, defaultYPos, Time.deltaTime * 5f),
                playerCamera.transform.localPosition.z);
        }
    }

    private void HandleFootsteps()
    {
        if (!characterController.isGrounded || moveDirection.magnitude < 0.1f) return;

        footstepTimer -= Time.deltaTime;

        if (footstepTimer <= 0)
        {
            if (footstepAudioSource != null && footstepSounds != null && footstepSounds.Length > 0)
            {
                footstepAudioSource.PlayOneShot(footstepSounds[Random.Range(0, footstepSounds.Length)]);
            }

            footstepTimer = baseStepSpeed * (isCrouching ? crouchStepMultiplier : isSprinting ? sprintStepMultiplier : 1f);
        }
    }

    public bool IsSprinting => isSprinting && currentInput.magnitude > 0;
    public bool IsCrouching => isCrouching;
    public float GetCurrentSpeed() => characterController.velocity.magnitude;

    // --- NEW: WAKE UP CINEMATIC ---
    private System.Collections.IEnumerator WakeUpSequence()
    {
        isWakingUp = true;
        
        // 1. Initial State: Lying on the floor, head tilted
        playerCamera.transform.localPosition = new Vector3(0, 0.2f, 0);
        playerCamera.transform.localRotation = Quaternion.Euler(0, 0, 75f);
        
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.color = new Color(0, 0, 0, 1f); // Pitch black
        }

        // Wait in darkness for 2 seconds
        yield return new WaitForSeconds(2f);

        // Blink 1: Open slightly
        yield return FadeScreen(1f, 0.4f, 0.5f);
        // Blink 1: Close
        yield return FadeScreen(0.4f, 1f, 0.3f);
        
        yield return new WaitForSeconds(0.5f);

        // Blink 2: Open fully while starting to get up
        StartCoroutine(FadeScreen(1f, 0f, 2f));
        
        // Dheeray dheeray khara hona (Stand up animation via code - MORE REALISTIC)
        
        // Phase 1: Zameen se uth kar ghutnon (knees) par aana aur neechay dekhna
        float duration1 = 2f;
        float elapsed = 0f;
        Vector3 startPos = playerCamera.transform.localPosition;
        Quaternion startRot = playerCamera.transform.localRotation;
        
        Vector3 midPos = new Vector3(0, defaultYPos * 0.4f, 0);
        Quaternion midRot = Quaternion.Euler(40f, 0, 10f); // Looking down at the floor, slightly tilted

        while (elapsed < duration1)
        {
            float t = elapsed / duration1;
            t = t * t * (3f - 2f * t); // SmoothStep for natural movement
            playerCamera.transform.localPosition = Vector3.Lerp(startPos, midPos, t);
            playerCamera.transform.localRotation = Quaternion.Lerp(startRot, midRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Take a deep breath (Pause)
        yield return new WaitForSeconds(1f);

        // Phase 2: Poori tarah khara hona (Stand up fully with a slight wobble/struggle)
        float duration2 = 2.5f;
        elapsed = 0f;
        startPos = playerCamera.transform.localPosition;
        startRot = playerCamera.transform.localRotation;
        
        Vector3 targetPos = new Vector3(0, defaultYPos, 0);
        Quaternion targetRot = Quaternion.Euler(0, 0, 0); // Straight head

        while (elapsed < duration2)
        {
            float t = elapsed / duration2;
            float smoothT = t * t * (3f - 2f * t); // SmoothStep
            
            // Camera wobble effect to simulate struggling to stand
            float wobble = Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * 3f; 
            
            playerCamera.transform.localPosition = Vector3.Lerp(startPos, targetPos, smoothT);
            playerCamera.transform.localRotation = Quaternion.Lerp(startRot, targetRot, smoothT) * Quaternion.Euler(wobble, 0, wobble);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        playerCamera.transform.localPosition = targetPos;
        playerCamera.transform.localRotation = targetRot;

        if (fadeImage != null) fadeImage.gameObject.SetActive(false);
        
        isWakingUp = false; // Player can now move!
    }

    private System.Collections.IEnumerator FadeScreen(float startAlpha, float targetAlpha, float duration)
    {
        if (fadeImage == null) yield break;

        float elapsed = 0f;
        Color color = fadeImage.color;
        
        while (elapsed < duration)
        {
            color.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            fadeImage.color = color;
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        color.a = targetAlpha;
        fadeImage.color = color;
    }
}
