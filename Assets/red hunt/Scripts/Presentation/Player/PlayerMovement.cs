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

        // ✅ Asegurar gravedad al activar el jugador
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
        // Si el jugador local está en pausa, no procesar input ni look
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
            Debug.Log($"[PlayerMovement] ✅ Input detectado: Move={moveInput}, Look={lookInput}");
            hasLoggedInputCheck = true;
        }

        HandleMove(moveInput);
        HandleLook(lookInput);
    }

    private void FixedUpdate()
    {
        if (LevelManager.IsLocallyPaused) return;

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

        Debug.Log("[PlayerMovement] JUMP activado");
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

        cameraHolder.localPosition = Vector3.up * 0.6f;
    }

    public bool GetIsGrounded() => isGrounded;
}