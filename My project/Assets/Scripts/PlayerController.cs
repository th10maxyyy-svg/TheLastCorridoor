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

    private CharacterController characterController;
    private Camera playerCamera;
    private PlayerStats playerStats;

    private Vector3 moveDirection;
    private Vector2 currentInput;
    private float rotationX = 0;
    private float currentSpeed;
    private bool isCrouching;
    private bool isSprinting;
    
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
    }

    private float hideTime;

    void Update()
    {
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

    public bool IsSprinting => isSprinting && currentInput.magnitude > 0;
    public bool IsCrouching => isCrouching;
    public float GetCurrentSpeed() => characterController.velocity.magnitude;
}
