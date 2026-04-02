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
    private Transform cameraHolder;  // ⭐ GameObject vacío que rota arriba/abajo
    private float xRotation = 0f;
    private bool isGrounded = true;
    
    private bool hasLoggedInputCheck = false; 

    public Vector3 CurrentVelocity => rb.linearVelocity;
    public bool IsJumping => !isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        inputHandler = GetComponent<PlayerInputHandler>();
        
        // ⭐ CAMBIO: Buscar CameraHolder en lugar de Camera directamente
        cameraHolder = transform.Find("CameraHolder");
        if (cameraHolder == null)
        {
            Debug.LogWarning("[PlayerMovement] ⚠️ CameraHolder no encontrado. Creando uno...");
            
            // Si no existe, crear GameObject vacío
            GameObject holderGO = new GameObject("CameraHolder");
            holderGO.transform.SetParent(transform);
            holderGO.transform.localPosition = Vector3.up * 0.6f;
            holderGO.transform.localRotation = Quaternion.identity;
            cameraHolder = holderGO.transform;
            
            Debug.Log("[PlayerMovement] ✅ CameraHolder creado automáticamente");
        }
        else
        {
            Debug.Log($"[PlayerMovement] ✅ CameraHolder encontrado: {cameraHolder.gameObject.name}");
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
            Debug.LogError("[PlayerMovement] ❌ CameraHolder no pudo ser creado");
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

        Vector2 moveInput = inputHandler.GetMoveInput();
        Vector2 lookInput = inputHandler.GetLookInput();

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
        moveVelocity.y = rb.linearVelocity.y;

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
        if (cameraHolder == null || lookInput == Vector2.zero) return;

        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        // ⭐ ROTACIÓN VERTICAL: Aplicar al CameraHolder (pitch)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);
        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        
        // ⭐ ROTACIÓN HORIZONTAL: Aplicar al cuerpo del jugador (yaw)
        transform.Rotate(Vector3.up * mouseX);

        Debug.Log($"[PlayerMovement] 📷 Look: xRot={xRotation:F1}°, mouseX={mouseX:F2}, mouseY={mouseY:F2}");
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
        if (cameraHolder == null) return;

        // ⭐ Mantener el CameraHolder en la posición correcta (0.6 arriba del jugador)
        cameraHolder.localPosition = Vector3.up * 0.6f;
    }

    public bool GetIsGrounded() => isGrounded;
}