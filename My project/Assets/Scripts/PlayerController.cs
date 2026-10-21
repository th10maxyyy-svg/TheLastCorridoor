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
        if (currentExitPos != null) transform.position = currentExitPos.position;
        characterController.enabled = true;
        isHidden = false;
        currentExitPos = null;
    }

    private void HandleMovementInput()
    {
        float moveDirectionY = moveDirection.y;
        currentInput = new Vector2(Input.GetAxis("Vertical"), Input.GetAxis("Horizontal"));

        // Bina stamina ke sprint nahi (warna footsteps/headbob/DDA sab "sprint" samajhte the jab ke player chal raha tha)
        isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching && currentInput.x > 0 && (playerStats == null || playerStats.HasStamina());

        if (isCrouching)
        {
            currentSpeed = crouchSpeed;
        }
        else if (isSprinting)
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
            // Neechi chhat ke neeche khare nahi ho sakte (warna player chhat mein phans jata tha)
            if (isCrouching && !CanStandUp()) return;

            isCrouching = !isCrouching;
            // Purana crouch coroutine rok do taake jaldi jaldi Ctrl dabane par do coroutines aapas mein na larein
            if (crouchRoutine != null) StopCoroutine(crouchRoutine);
            crouchRoutine = StartCoroutine(CrouchStandRoutine());
        }
    }

    private Coroutine crouchRoutine;

    private bool CanStandUp()
    {
        if (characterController == null) return true;
        float radius = characterController.radius * 0.9f;
        Vector3 origin = transform.position + characterController.center;
        float distance = standingHeight * 0.5f - radius + 0.05f;

        foreach (RaycastHit hit in Physics.SphereCastAll(origin, radius, Vector3.up, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform)) return false; // something solid above the head
        }
        return true;
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
        crouchRoutine = null;
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

    public Animator BodyAnimator => bodyAnimator;
    public bool IsSprinting => isSprinting && currentInput.magnitude > 0;
    public bool IsCrouching => isCrouching;
    public float GetCurrentSpeed() => characterController.velocity.magnitude;

    /// <summary>
    /// Failsafe used after a death-respawn scene reload: guarantees the player is never left frozen
    /// by a cinematic flag/controller state.
    /// </summary>
    public void ForceUnlock()
    {
        StopAllCoroutines();
        crouchRoutine = null;
        isWakingUp = false;
        isCinematicPlaying = false;
        isHidden = false;
        enabled = true;
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (characterController != null) characterController.enabled = true;
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
            fadeImage.raycastTarget = false;
            fadeImage.gameObject.SetActive(false);
        }
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

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

    // --- the player's own body is hidden during the basement jumpscare + drag (you only see the clown) ---
    private readonly System.Collections.Generic.List<Renderer> hiddenBodyRenderers = new System.Collections.Generic.List<Renderer>();
    public void SetBodyVisible(bool visible)
    {
        if (!visible)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                if (r.enabled && !hiddenBodyRenderers.Contains(r)) { r.enabled = false; hiddenBodyRenderers.Add(r); }
        }
        else
        {
            foreach (Renderer r in hiddenBodyRenderers) if (r != null) r.enabled = true;
            hiddenBodyRenderers.Clear();
        }
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
        SetBodyVisible(false); // never show the player's own legs/arms while he is being dragged
        if (characterController != null) characterController.enabled = false;
        if (fadeImage != null) fadeImage.gameObject.SetActive(true);

        // NAYA: Saans zor se phoolay gi behoshi ke doran
        if (playerStats != null)
        {
            playerStats.TriggerHeavyBreathing(10f);
        }
        
        Animator draggerAnim = null;
        Vector3 dragDir = -transform.forward;
        dragDir.y = 0f;
        if (dragger != null)
        {
            // Enemy ko normal AI se rok kar dragging scene ka hissa bana dein!
            UnityEngine.AI.NavMeshAgent agent = dragger.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            EnemyAI enemyAI = dragger.GetComponent<EnemyAI>();
            if (enemyAI != null) enemyAI.enabled = false;

            // He walks AWAY from the player, pulling him along: the direction is from the player towards the enemy
            Vector3 toEnemy = dragger.transform.position - transform.position;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude > 0.01f) dragDir = toEnemy.normalized;

            // REAL walk animation: switch the animator straight to "Walking" (it used to stay in the yell pose, so he slid like a dummy)
            draggerAnim = (enemyAI != null && enemyAI.enemyAnimator != null) ? enemyAI.enemyAnimator : dragger.GetComponentInChildren<Animator>(true);
            if (draggerAnim != null && (draggerAnim.runtimeAnimatorController == null || !draggerAnim.isActiveAndEnabled))
            {
                Debug.LogWarning("[Drag] enemy Animator has no controller / is inactive: " + draggerAnim.name + " ctrl=" + (draggerAnim.runtimeAnimatorController != null) + " active=" + draggerAnim.isActiveAndEnabled);
                draggerAnim = null;
            }
            if (draggerAnim != null)
            {
                draggerAnim.speed = 1f;
                draggerAnim.ResetTrigger("Tease");
                draggerAnim.ResetTrigger("Attack");
                draggerAnim.SetFloat("Speed", 1.6f);
                draggerAnim.CrossFadeInFixedTime("Walking", 0.25f);
            }
            CinematicAmbush.AmbushLog("drag start: dragger=" + dragger.name + " pos=" + dragger.transform.position + " walkAnim=" + (draggerAnim != null ? draggerAnim.name : "NONE"));
            if (enemyAI != null) enemyAI.KeepGrounded();
        }
        if (dragDir.sqrMagnitude < 0.01f) dragDir = transform.forward;
        dragDir.Normalize();

        // 1. He grabs the player's leg and steps AHEAD of him, so while dragging you clearly SEE him walking away with you
        //    (before he stayed 0.7 m away and the low camera only saw the top of his skull)
        EnemyAI draggerAI = dragger != null ? dragger.GetComponent<EnemyAI>() : null;
        float floorY = transform.position.y - 1f;
        {
            RaycastHit floorHit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out floorHit, 6f, ~0, QueryTriggerInteraction.Ignore))
                floorY = floorHit.point.y;
        }
        Vector3 enemyFrom = dragger != null ? dragger.transform.position : Vector3.zero;
        Vector3 enemyTo = enemyFrom;
        if (dragger != null)
        {
            Vector3 p = transform.position; p.y = enemyFrom.y;
            enemyTo = p + dragDir * 2.0f;
        }

        // the player falls on his back: camera on the floor, looking up along his body at the clown
        float duration = 0.55f;
        float elapsed = 0f;
        Vector3 camStartPos = playerCamera.transform.position;
        Quaternion camStartRot = playerCamera.transform.rotation;
        Vector3 smoothLook = dragger != null ? dragger.transform.position + Vector3.up * 0.45f : transform.position + dragDir * 2f;
        if (fadeImage != null) StartCoroutine(FadeScreen(0f, 0.45f, 0.35f));   // hit the floor: vision goes dark for a moment
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);
            if (dragger != null)
            {
                dragger.transform.position = Vector3.Lerp(enemyFrom, enemyTo, t);
                dragger.transform.rotation = Quaternion.Slerp(dragger.transform.rotation, Quaternion.LookRotation(dragDir), t);
                if (draggerAI != null) draggerAI.KeepGrounded();
            }
            Vector3 camFloor = transform.position - dragDir * 0.25f; camFloor.y = floorY + 0.32f;
            Vector3 target = dragger != null ? dragger.transform.position + Vector3.up * 0.45f : transform.position + dragDir * 2f;
            smoothLook = Vector3.Lerp(smoothLook, target, 0.25f);
            Quaternion lookRot = Quaternion.LookRotation((smoothLook - camFloor).normalized) * Quaternion.Euler(0f, 0f, 18f);
            playerCamera.transform.position = Vector3.Lerp(camStartPos, camFloor, t);
            playerCamera.transform.rotation = Quaternion.Slerp(camStartRot, lookRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (fadeImage != null) StartCoroutine(FadeScreen(0.45f, 0.1f, 0.5f));    // eyes open again: watch him drag you


        // 2. The drag: he walks away holding the leg, the player slides behind him, the head bumps along the floor
        float dragDuration = 3.4f;
        elapsed = 0f;
        bool blackoutStarted = false;
        while (elapsed < dragDuration)
        {
            float walk = 1.25f * Mathf.Clamp01(elapsed / 0.5f);
            Vector3 moveDelta = dragDir * walk * Time.deltaTime;

            // never drag through a wall
            if (dragger != null && Physics.Raycast(dragger.transform.position + Vector3.up * 0.2f, dragDir, 0.8f, ~0, QueryTriggerInteraction.Ignore))
                moveDelta = Vector3.zero;

            transform.position += moveDelta;
            if (dragger != null)
            {
                dragger.transform.position += moveDelta;
                dragger.transform.rotation = Quaternion.Slerp(dragger.transform.rotation, Quaternion.LookRotation(dragDir), Time.deltaTime * 6f);
                if (draggerAI != null) draggerAI.KeepGrounded();
                if (draggerAnim != null) draggerAnim.SetFloat("Speed", moveDelta.sqrMagnitude > 0f ? 1.6f : 0f);
            }

            // camera: on the floor behind the player's body, looking up at the clown's back; small bumps with every pull
            float pull = Mathf.Abs(Mathf.Sin(elapsed * 4.2f));
            Vector3 camFloor = transform.position - dragDir * 0.25f;
            camFloor.y = floorY + 0.32f + pull * 0.03f;
            Vector3 target = dragger != null ? dragger.transform.position + Vector3.up * 0.45f : transform.position + dragDir * 2f;
            smoothLook = Vector3.Lerp(smoothLook, target, Time.deltaTime * 8f);
            playerCamera.transform.position = camFloor;
            playerCamera.transform.rotation = Quaternion.LookRotation((smoothLook - camFloor).normalized)
                                             * Quaternion.Euler(-pull * 1.5f, Mathf.Sin(elapsed * 1.3f) * 3f, 18f + Mathf.Sin(elapsed * 2.1f) * 4f);

            // he drags you into the dark: you pass out near the end
            if (!blackoutStarted && elapsed > dragDuration - 1.1f)
            {
                blackoutStarted = true;
                StartCoroutine(FadeScreen(0.1f, 1f, 1.0f));
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
            if (enemyAI != null)
            {
                enemyAI.enabled = true;
                enemyAI.ForcePatrol(); // Zabardasti patrol karwao taake tease se bahir aa jaye!
            }
        }

        // 3. Teleport to Basement
        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation;

        // SAVE RIGHT NOW: the player is in the basement. If the game is closed at any moment from here on,
        // Resume puts him back in the basement (before, the last autosave was still upstairs).
        // The drop point is at floor level - RespawnManager adds +1 m when it places the player, exactly as for this point.
        if (SaveManager.instance != null) SaveManager.instance.SavePlayerTransform(spawnPoint.position, spawnPoint.rotation);

        // Reset stats!
        if (playerStats != null)
        {
            playerStats.LoadStats(100f, 100f);
        }
        isDead = false;

        // 4. Wake Up Sequence! (the body is shown again once he is in the basement)
        SetBodyVisible(true);
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
