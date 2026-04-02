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

    [Header("Gravedad")]
    [SerializeField] private bool enableGravity = true;

    private Rigidbody rb;
    private PlayerInputHandler inputHandler;
    private Transform cameraTransform;
    private float xRotation = 0f;
    private bool isGrounded = true;
    
    private bool hasLoggedInputCheck = false; 

    public Vector3 CurrentVelocity => rb.linearVelocity;
    public bool IsJumping => !isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        inputHandler = GetComponent<PlayerInputHandler>();
        cameraTransform = Camera.main?.transform;

        if (rb == null)
        {
            Debug.LogError("[PlayerMovement] Rigidbody no encontrado");
        }

        if (inputHandler == null)
        {
            Debug.LogWarning("[PlayerMovement] PlayerInputHandler no encontrado");
        }

        if (cameraTransform == null)
        {
            Debug.LogWarning("[PlayerMovement] Cámara principal no encontrada");
        }
    }

    public void SetGravityEnabled(bool enabled)
    {
        enableGravity = enabled;
        if (rb != null)
        {
            rb.useGravity = enabled;
            if (!enabled)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        Debug.Log($"[PlayerMovement] Gravedad: {(enabled ? "ACTIVADA" : "DESACTIVADA")}");
    }

    private void OnEnable()
    {
        if (inputHandler != null)
        {
            inputHandler.OnJump += HandleJump;
            Debug.Log("[PlayerMovement] ✅ Evento OnJump suscrito");
        }
        hasLoggedInputCheck = false;
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
        if (inputHandler == null)
        {
            if (!hasLoggedInputCheck)
            {
                Debug.LogWarning("[PlayerMovement] ⚠️ inputHandler es NULL");
                hasLoggedInputCheck = true;
            }
            return;
        }

        // ⭐ DEBUG: Verificar input cada frame
        Vector2 moveInput = inputHandler.GetMoveInput();
        Vector2 lookInput = inputHandler.GetLookInput();

        // Log una sola vez cuando hay input
        if (!hasLoggedInputCheck && (moveInput != Vector2.zero || lookInput != Vector2.zero))
        {
            Debug.Log($"[PlayerMovement] ✅ Input detectado: Move={moveInput}, Look={lookInput}");
            hasLoggedInputCheck = true;
        }

        HandleMove(moveInput);
        HandleLook(lookInput);
    }

    private void FixedUpdate()
    {
        CheckGroundStatus();
        ApplyDrag();
    }

    private void LateUpdate()
    {
        UpdateCameraPosition();
    }

    private void HandleMove(Vector2 moveInput)
    {
        if (rb == null) return;



        Vector3 moveDirection = Vector3.zero;

        if (moveInput.y > 0) // W - Adelante
            moveDirection += transform.forward;
        if (moveInput.y < 0) // S - Atrás
            moveDirection -= transform.forward;
        if (moveInput.x > 0) // D - Derecha
            moveDirection += transform.right;
        if (moveInput.x < 0) // A - Izquierda
            moveDirection -= transform.right;

        moveDirection = moveDirection.normalized;

        Vector3 moveVelocity = moveDirection * moveSpeed;
        moveVelocity.y = rb.linearVelocity.y; // Preservar velocidad vertical

        rb.linearVelocity = moveVelocity;
    }

    private void HandleJump()
    {
        if (rb == null || !isGrounded || !enableGravity) return;

        Debug.Log("[PlayerMovement] JUMP activado");
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private void HandleLook(Vector2 lookInput)
    {
        if (cameraTransform == null || lookInput == Vector2.zero) return;

        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
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

    private void UpdateCameraPosition()
    {
        if (cameraTransform == null) return;

        cameraTransform.position = transform.position + Vector3.up * 0.6f;
    }

    public bool GetIsGrounded() => isGrounded;
}