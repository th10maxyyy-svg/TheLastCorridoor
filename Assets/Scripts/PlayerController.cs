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

    // References
    private CharacterController characterController;
    private Camera playerCamera;
    private PlayerStats playerStats;

    // Movement state
    private Vector3 moveDirection;
    private Vector2 currentInput;
    private float rotationX = 0;
    private float currentSpeed;
    private bool isCrouching;
    private bool isSprinting;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        playerStats = GetComponent<PlayerStats>();

        // Lock cursor to center of screen and make it invisible
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentSpeed = walkSpeed;
    }

    void Update()
    {
        HandleMovementInput();
        HandleMouseLook();
        HandleCrouch();
        ApplyMovement();
    }

    private void HandleMovementInput()
    {
        // Get input axes (WASD / Arrow keys)
        float moveDirectionY = moveDirection.y;
        currentInput = new Vector2(Input.GetAxis("Vertical"), Input.GetAxis("Horizontal"));

        // Determine Speed mode: Crouch -> Sprint -> Walk
        isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching && currentInput.x > 0; // Sprint only forward

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

        // Calculate direction based on forward and right vectors
        Vector3 moveForward = transform.TransformDirection(Vector3.forward) * currentInput.x;
        Vector3 moveRight = transform.TransformDirection(Vector3.right) * currentInput.y;
        
        moveDirection = (moveForward + moveRight).normalized * currentSpeed;
        moveDirection.y = moveDirectionY; // Restore vertical movement (gravity/jump)
    }

    private void HandleMouseLook()
    {
        if (playerCamera == null) return;

        // Rotation around Y axis (horizontal mouse movement turns player body)
        transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * mouseSensitivity);

        // Rotation around X axis (vertical mouse movement tilts camera)
        rotationX -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        rotationX = Mathf.Clamp(rotationX, upperLookLimit, lowerLookLimit);
        
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
    }

    private void HandleCrouch()
    {
        // Toggle crouch with Left Control key
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

        // Ensure final values are exact
        characterController.height = targetHeight;
        playerCamera.transform.localPosition = targetCamPosition;
    }

    private void ApplyMovement()
    {
        // Apply Gravity if not grounded
        if (!characterController.isGrounded)
        {
            moveDirection.y += gravity * Time.deltaTime;
        }
        else
        {
            // Reset gravity value when on ground
            if (moveDirection.y < 0)
                moveDirection.y = -2.0f;

            // Handle Jump
            if (Input.GetButtonDown("Jump"))
            {
                moveDirection.y = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
            }
        }

        // Move the CharacterController
        characterController.Move(moveDirection * Time.deltaTime);
    }

    // Helper properties for other scripts (like HUD or Footsteps)
    public bool IsSprinting => isSprinting && currentInput.magnitude > 0;
    public bool IsCrouching => isCrouching;
    public float GetCurrentSpeed() => characterController.velocity.magnitude;
}
