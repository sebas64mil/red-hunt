using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float groundDrag = 5f;
    [SerializeField] private float airDrag = 2f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private float maxLookAngle = 90f;

    [Header("Suelo")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.2f;

    [Header("Cámara")]
    [SerializeField] private Transform cameraHolder;  // ⭐ CAMBIO: Ahora es [SerializeField]

    [Header("Posición de la Cámara por Estado")]
    [SerializeField] private Vector3 cameraPositionIdle = new Vector3(0f, 0.6f, 0f);
    [SerializeField] private Vector3 cameraPositionWalk = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private float cameraSmoothSpeed = 5f;

    private Rigidbody rb;
    private PlayerInputHandler inputHandler;
    private PlayerAnimationController animationController;
    private float xRotation = 0f;
    private bool isGrounded = true;
    
    private bool hasLoggedInputCheck = false; 
    private int previousMovementState = -1;

    public Vector3 CurrentVelocity => rb.linearVelocity;
    public bool IsJumping => !isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        inputHandler = GetComponent<PlayerInputHandler>();
        animationController = GetComponent<PlayerAnimationController>();
        
        if (cameraHolder == null)
        {
            cameraHolder = transform.Find("CameraHolder");
        }
        else
        {
            Debug.Log($"[PlayerMovement] ✅ CameraHolder asignado: {cameraHolder.gameObject.name}");
        }

        if (rb == null)
        {
            Debug.LogError("[PlayerMovement] Rigidbody no encontrado");
        }

        if (inputHandler == null)
        {
            Debug.LogWarning("[PlayerMovement] PlayerInputHandler no encontrado");
        }

        if (cameraHolder == null)
        {
            Debug.LogError("[PlayerMovement] ❌ CameraHolder no pudo ser asignado");
        }

        if (rb != null)
        {
            rb.useGravity = true;
        }
    }

    private void OnEnable()
    {
        if (inputHandler != null)
        {
            inputHandler.OnJump += HandleJump;
            Debug.Log("[PlayerMovement] ✅ Evento OnJump suscrito");
        }
        hasLoggedInputCheck = false;

        if (rb != null)
        {
            rb.useGravity = true;
        }
    }

    private void OnDisable()
    {
        if (inputHandler != null)
        {
            inputHandler.OnJump -= HandleJump;
        }
    }

    private void Update()
    {
        if (LevelManager.IsLocallyPaused) return;

        if (inputHandler == null)
        {
            if (!hasLoggedInputCheck)
            {
                Debug.LogWarning("[PlayerMovement] ⚠️ inputHandler es NULL");
                hasLoggedInputCheck = true;
            }
            return;
        }

        Vector2 moveInput = inputHandler.GetMoveInput();
        Vector2 lookInput = inputHandler.GetLookInput();

        if (!hasLoggedInputCheck && (moveInput != Vector2.zero || lookInput != Vector2.zero))
        {
            hasLoggedInputCheck = true;
        }

        HandleMove(moveInput);
        HandleLook(lookInput);

        // Llamar al controlador de animaciones con la velocidad actual
        if (animationController != null)
        {
            animationController.UpdateMovementAnimation(CurrentVelocity);
            UpdateCameraHolderPosition();
        }
    }

    private void FixedUpdate()
    {
        if (LevelManager.IsLocallyPaused) return;

        CheckGroundStatus();
        ApplyDrag();
    }

    private void HandleMove(Vector2 moveInput)
    {
        if (rb == null) return;

        Vector3 moveDirection = Vector3.zero;

        if (moveInput.y > 0) 
            moveDirection += transform.forward;
        if (moveInput.y < 0) 
            moveDirection -= transform.forward;
        if (moveInput.x > 0) 
            moveDirection += transform.right;
        if (moveInput.x < 0) 
            moveDirection -= transform.right;

        moveDirection = moveDirection.normalized;

        Vector3 moveVelocity = moveDirection * moveSpeed;
        moveVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = moveVelocity;
    }

    private void HandleJump()
    {
        if (LevelManager.IsLocallyPaused) return;

        if (rb == null || !isGrounded) return;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private void HandleLook(Vector2 lookInput)
    {
        if (LevelManager.IsLocallyPaused) return;

        if (cameraHolder == null || lookInput == Vector2.zero) return;

        float lookX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float lookY = lookInput.y * mouseSensitivity * Time.deltaTime;

        xRotation -= lookY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);
        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        
        transform.Rotate(Vector3.up * lookX);
    }

    private void UpdateCameraHolderPosition()
    {
        if (cameraHolder == null || animationController == null) return;

        int currentMovementState = animationController.GetCurrentMovementState();

        // Solo actualizar si el estado cambió para evitar cálculos innecesarios
        if (previousMovementState != currentMovementState)
        {
            previousMovementState = currentMovementState;
        }

        Vector3 targetPosition = currentMovementState == 1 ? cameraPositionWalk : cameraPositionIdle;
        cameraHolder.localPosition = Vector3.Lerp(cameraHolder.localPosition, targetPosition, cameraSmoothSpeed * Time.deltaTime);
    }

    private void CheckGroundStatus()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
    }

    private void ApplyDrag()
    {
        if (rb == null) return;

        rb.linearDamping = isGrounded ? groundDrag : airDrag;
    }

    public bool GetIsGrounded() => isGrounded;
}