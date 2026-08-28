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
    [SerializeField] private float lowerLookLimit = 50.0f; // Restrict downward look to avoid neck clipping

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

    [Header("Immersive Audio (Muttering)")]
    [SerializeField] private AudioSource mutteringAudioSource;
    [SerializeField] private AudioClip[] mutteringSounds;
    [SerializeField] private float minMutterDelay = 60f;
    [SerializeField] private float maxMutterDelay = 120f;
    
    [Header("First Person Body")]
    [SerializeField] private Animator bodyAnimator;

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
    private float mutterTimer = 0f;
    
    public bool isHidden { get; private set; }
    private Transform currentExitPos;
    public bool isDead { get; private set; }
    [HideInInspector] public bool isCinematicPlaying = false;



    void Start()
    {
        characterController = GetComponent<CharacterController>();
        // Ensure player is thin enough to pass through doors easily!
        if (characterController != null) characterController.radius = 0.3f;
        
        playerCamera = GetComponentInChildren<Camera>();
        playerStats = GetComponent<PlayerStats>();

        // GUARANTEED FIX: Agar 'remy' ya uske parts ghalti se disable ho gaye hain to unhe automatically on karo
        Transform remyTransform = transform.Find("remy");
        if (remyTransform != null)
        {
            remyTransform.gameObject.SetActive(true);
            SkinnedMeshRenderer[] meshes = remyTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer smr in meshes)
            {
                smr.gameObject.SetActive(true);
                smr.enabled = true;
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentSpeed = walkSpeed;
        if (playerCamera != null)
        {
            // FORCE CAMERA FIXES OVERRIDING INSPECTOR
            Vector3 camPos = playerCamera.transform.localPosition;
            camPos.z = 0.35f; // Aagay push karo taake chhaati bilkul cross ho jaye
            playerCamera.transform.localPosition = camPos;
            playerCamera.nearClipPlane = 0.01f; // Clipping theek karo
            
            // Camera ki Editor wali height ko hi standing height bana dein
            standingCameraPosition.y = camPos.y;
            defaultYPos = camPos.y;
        }

        // FORCE LOOK LIMIT
        lowerLookLimit = 50f;

        // AUTO-FIX: Automatically add AudioSource for footsteps if not assigned
        if (footstepAudioSource == null)
        {
            footstepAudioSource = gameObject.AddComponent<AudioSource>();
            footstepAudioSource.spatialBlend = 0f; // 2D sound for first person
        }

        if (playWakeUpCinematic)
        {
            if (fadeImage != null) fadeImage.raycastTarget = false; // GUARANTEED FIX: Fade UI ko click block karne se roko
            StartCoroutine(WakeUpSequence());
        }

        mutterTimer = Time.time + Random.Range(minMutterDelay, maxMutterDelay);
    }

    private float hideTime;

    void Update()
    {
        if (isDead)
        {
            HandleDeathSway();
            return;
        }

        if (isWakingUp || isCinematicPlaying) return; // Paralyze player during cinematic

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
        HandleMuttering();
        UpdateBodyAnimations();
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
        float targetCamY = isCrouching ? crouchingCameraPosition.y : standingCameraPosition.y;
        float currentHeight = characterController.height;
        Vector3 currentCamPos = playerCamera.transform.localPosition;
        
        // CAMERA KI X aur Z position wohi rakho jo editor mein set ki thi (Sirf Y change karo)
        Vector3 targetCamPos = new Vector3(currentCamPos.x, targetCamY, currentCamPos.z);

        float elapsedTime = 0f;

        while (elapsedTime < timeToCrouch)
        {
            characterController.height = Mathf.Lerp(currentHeight, targetHeight, elapsedTime / timeToCrouch);
            playerCamera.transform.localPosition = Vector3.Lerp(currentCamPos, targetCamPos, elapsedTime / timeToCrouch);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        characterController.height = targetHeight;
        playerCamera.transform.localPosition = targetCamPos;
        
        // Update defaultYPos for headbob base calculation
        defaultYPos = targetCamY;
    }

    private void ApplyMovement()
    {
        if (characterController == null || !characterController.enabled) return;

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
        // Vector3.y ko ignore kar ke sirf horizontal movement check karein (X aur Z axis)
        Vector2 horizontalVelocity = new Vector2(characterController.velocity.x, characterController.velocity.z);

        // Agar player hawa mein hai ya ruka hua hai, to aawaz foran band (Stop) kar do
        if (!characterController.isGrounded || horizontalVelocity.magnitude < 0.1f)
        {
            if (footstepAudioSource != null && footstepAudioSource.isPlaying)
            {
                footstepAudioSource.Stop();
            }
            return;
        }

        // Agar player chal raha hai
        if (footstepAudioSource != null && footstepSounds != null && footstepSounds.Length > 0)
        {
            // Pehli dafa aawaz set karna aur loop enable karna
            if (footstepAudioSource.clip == null)
            {
                // Jo pehli sound aapne daali hai (9 sec wali), wo set ho jayegi
                footstepAudioSource.clip = footstepSounds[0]; 
                footstepAudioSource.loop = true; // Taake wo 9 sec ke baad khud repeat ho
            }

            // Agar aawaz abhi nahi chal rahi, to shuru karo
            if (!footstepAudioSource.isPlaying)
            {
                footstepAudioSource.Play();
            }

            // Bhagne (Sprint) ke waqt aawaz ki speed (Pitch) bohat tez kar di hai taake bhagne wali feel aye
            footstepAudioSource.pitch = isSprinting ? 1.8f : (isCrouching ? 0.7f : 1.0f);
        }
    }

    private void HandleMuttering()
    {
        if (mutteringAudioSource == null || mutteringSounds == null || mutteringSounds.Length == 0) return;

        if (Time.time >= mutterTimer)
        {
            AudioClip mutter = mutteringSounds[Random.Range(0, mutteringSounds.Length)];
            mutteringAudioSource.PlayOneShot(mutter);
            mutterTimer = Time.time + Random.Range(minMutterDelay, maxMutterDelay);
        }
    }

    private void UpdateBodyAnimations()
    {
        if (bodyAnimator == null) return;

        bodyAnimator.SetBool("IsMoving", moveDirection.magnitude > 0.1f);
        bodyAnimator.SetBool("IsSprinting", isSprinting);
        bodyAnimator.SetBool("IsCrouching", isCrouching);
    }

    public void TriggerInteractAnimation()
    {
        if (bodyAnimator != null)
        {
            bodyAnimator.SetTrigger("InteractTrigger");
        }
    }

    public bool IsSprinting => isSprinting && currentInput.magnitude > 0;
    public bool IsCrouching => isCrouching;
    public float GetCurrentSpeed() => characterController.velocity.magnitude;

    public void TriggerWakeUpSequence()
    {
        if (fadeImage != null) fadeImage.raycastTarget = false;
        StartCoroutine(WakeUpSequence());
    }

    // --- NEW: WAKE UP CINEMATIC ---
    private System.Collections.IEnumerator WakeUpSequence()
    {
        isWakingUp = true;
        
        // Start heavy breathing immediately upon waking up
        if (playerStats != null)
        {
            playerStats.TriggerHeavyBreathing(7f);
        }

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
        
        Vector3 midPos = new Vector3(0, defaultYPos * 0.4f, 0.35f); // FIX Z
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
        
        Vector3 targetPos = new Vector3(0, defaultYPos, 0.35f); // FIX Z
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
        if (characterController != null) characterController.enabled = true; // RE-ENABLE THE CONTROLLER SO THEY CAN WALK!
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

    // --- NEW: DRAG TO BASEMENT CINEMATIC ---
    public void TriggerDragToBasementCinematic(Transform spawnPoint, GameObject dragger = null)
    {
        if (spawnPoint == null)
        {
            Debug.LogError("PlayerController: Basement Spawn Point missing! Drag sequence failed.");
            return;
        }
        StartCoroutine(DragToBasementSequence(spawnPoint, dragger));
    }

    private System.Collections.IEnumerator DragToBasementSequence(Transform spawnPoint, GameObject dragger)
    {
        isWakingUp = true; // Paralyze player Update loop immediately!
        if (characterController != null) characterController.enabled = false;
        if (fadeImage != null) fadeImage.gameObject.SetActive(true);

        // NAYA: Saans zor se phoolay gi behoshi ke doran
        if (playerStats != null)
        {
            playerStats.TriggerHeavyBreathing(10f);
        }
        
        if (dragger != null)
        {
            // Enemy ko normal AI se rok kar dragging scene ka hissa bana dein!
            UnityEngine.AI.NavMeshAgent agent = dragger.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            
            EnemyAI enemyAI = dragger.GetComponent<EnemyAI>();
            if (enemyAI != null) enemyAI.enabled = false;
            
            Animator draggerAnim = dragger.GetComponentInChildren<Animator>();
            if (draggerAnim != null) draggerAnim.SetFloat("Speed", 1.5f); // Walk animation while dragging
        }

        // 1. Fall Down (Drop to floor quickly)
        float duration = 0.6f;
        float elapsed = 0f;
        Vector3 startPos = playerCamera.transform.localPosition;
        Quaternion startRot = playerCamera.transform.localRotation;
        
        Vector3 targetPos = new Vector3(0, 0.2f, 0.35f); 
        Quaternion targetLocalRot = Quaternion.Euler(-10f, startRot.eulerAngles.y, 75f); // Tilted head
        
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); 
            playerCamera.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            playerCamera.transform.localRotation = Quaternion.Lerp(startRot, targetLocalRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        playerCamera.transform.localPosition = targetPos;
        playerCamera.transform.localRotation = targetLocalRot;

        // 2. The Drag (Slide backward while blinking)
        float dragDuration = 2.5f; // Faster fade to avoid seeing wall clipping!
        elapsed = 0f;
        
        // Start fading in and out to simulate blinking/fainting
        StartCoroutine(FadeScreen(0f, 0.8f, 0.8f)); // Fade almost to black quickly
        
        while (elapsed < dragDuration)
        {
            // Drag backward slowly
            Vector3 moveDelta = -transform.forward * 1.5f * Time.deltaTime;
            transform.position += moveDelta;
            
            // Enemy (Dragger) ko bhi saath peeche kheench kar le jao taake lagay wo drag kar raha hai!
            if (dragger != null)
            {
                dragger.transform.position += moveDelta;
                // Force him to look at player
                dragger.transform.rotation = Quaternion.Slerp(dragger.transform.rotation, Quaternion.LookRotation(transform.position - dragger.transform.position), Time.deltaTime * 10f);
            }
            
            // Eye blink logic
            if (elapsed > 0.8f && elapsed < 0.9f)
            {
                StartCoroutine(FadeScreen(0.8f, 0.3f, 0.5f)); // Open eyes a bit
            }
            if (elapsed > 1.4f && elapsed < 1.5f)
            {
                StartCoroutine(FadeScreen(0.3f, 1f, 1f)); // Close fully (Blackout)
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Wait in complete darkness
        yield return new WaitForSeconds(1.5f);
        
        if (dragger != null)
        {
            // Enemy ko band nahi karna! Wapas NavMesh par rakh kar AI chalu karni hai taake wo 1st floor par ghoomta rahay.
            UnityEngine.AI.NavMeshAgent agent = dragger.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(dragger.transform.position, out hit, 10.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    dragger.transform.position = hit.position;
                }
                agent.enabled = true;
            }
            
            EnemyAI enemyAI = dragger.GetComponent<EnemyAI>();
            if (enemyAI != null) enemyAI.enabled = true;
        }

        // 3. Teleport to Basement
        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation;

        // Reset stats!
        if (playerStats != null)
        {
            playerStats.isScriptedDeath = false;
            playerStats.LoadStats(100f, 100f);
        }
        isDead = false;

        // 4. Wake Up Sequence!
        StartCoroutine(WakeUpSequence());
    }

    // --- NEW: DEATH CINEMATIC ---
    public void TriggerDeathCinematic()
    {
        if (!isDead)
        {
            isDead = true;
            StartCoroutine(DeathSequence());
        }
    }

    private Quaternion deathCamTargetRot;

    private void HandleDeathSway()
    {
        if (playerCamera == null) return;
        // Subtle mouse sway while lying on the floor
        float swayX = Input.GetAxis("Mouse X") * 0.2f; // Low sensitivity
        float swayY = Input.GetAxis("Mouse Y") * 0.2f;
        
        // Temporarily apply input
        playerCamera.transform.Rotate(Vector3.up * swayX, Space.World);
        playerCamera.transform.Rotate(Vector3.right * -swayY, Space.Self);
        
        // Gently pull camera back to the resting position so it doesn't drift away infinitely
        playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, deathCamTargetRot, Time.deltaTime * 2f);
    }

    private System.Collections.IEnumerator DeathSequence()
    {
        if (characterController != null) characterController.enabled = false;
        
        float duration = 0.6f; // Quick drop to the floor
        float elapsed = 0f;
        
        Vector3 startPos = playerCamera.transform.localPosition;
        Quaternion startRot = playerCamera.transform.localRotation;
        
        // Target: Fall to the floor, tilt head by 75 degrees, slightly look up
        Vector3 targetPos = new Vector3(0, 0.2f, 0.35f); // FIX Z
        Quaternion targetLocalRot = Quaternion.Euler(-10f, startRot.eulerAngles.y, 75f);
        
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // Ease out bounce or fast drop
            t = t * t * (3f - 2f * t); 
            
            playerCamera.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            playerCamera.transform.localRotation = Quaternion.Lerp(startRot, targetLocalRot, t);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        playerCamera.transform.localPosition = targetPos;
        playerCamera.transform.localRotation = targetLocalRot;
        
        // Save the world rotation for sway return
        deathCamTargetRot = playerCamera.transform.rotation;
    }
}
