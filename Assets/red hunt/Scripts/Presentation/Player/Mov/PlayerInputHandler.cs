using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    // ⭐ NUEVO: Evento de ataque
    public event Action OnAttack;

    // ⭐ SOLO evento de salto, los otros se leen directamente
    public event Action OnJump;

    // ⭐ NUEVO: Evento de interacción
    public event Action OnInteract;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction attackAction;
    private InputAction interactAction;

    private bool isInitialized = false;

    private void Awake()
    {
        InitializeInputActions();
    }

    private void InitializeInputActions()
    {
        if (isInitialized) return;

        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("[PlayerInputHandler] ❌ PlayerInput no encontrado en GameObject. Asegúrate de que existe.");
            return;
        }

        try
        {
            moveAction = playerInput.actions["Move"];
            lookAction = playerInput.actions["Look"];
            jumpAction = playerInput.actions["Jump"];
            attackAction = playerInput.actions["Attack"];
            interactAction = playerInput.actions["Interact"];

            if (moveAction == null || lookAction == null || jumpAction == null)
            {
                Debug.LogError("[PlayerInputHandler] ❌ InputActions no encontradas. Verifica que existan 'Move', 'Look' y 'Jump'");
                return;
            }

            // ⭐ NUEVO: Validar attack action (opcional para jugadores que no sean killers)
            if (attackAction == null)
            {
                Debug.LogWarning("[PlayerInputHandler] ⚠️ InputAction 'Attack' no encontrada. Los killers no podrán atacar.");
            }

            isInitialized = true;
            Debug.Log("[PlayerInputHandler] ✅ InputActions inicializadas correctamente");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayerInputHandler] ❌ Error inicializando InputActions: {ex.Message}");
        }
    }

    private void OnEnable()
    {
        if (!isInitialized)
        {
            InitializeInputActions();
        }

        if (moveAction != null)
        {
            moveAction.Enable();
            Debug.Log("[PlayerInputHandler] ✅ Move action habilitada");
        }

        if (lookAction != null)
        {
            lookAction.Enable();
            Debug.Log("[PlayerInputHandler] ✅ Look action habilitada");
        }

        if (jumpAction != null)
        {
            jumpAction.Enable();
            jumpAction.started += HandleJumpInput;
            Debug.Log("[PlayerInputHandler] ✅ Jump action habilitada");
        }

        // ⭐ NUEVO: Habilitar action de ataque
        if (attackAction != null)
        {
            attackAction.Enable();
            attackAction.started += HandleAttackInput;
            Debug.Log("[PlayerInputHandler] ✅ Attack action habilitada");
        }

        // ⭐ NUEVO: Habilitar action de interacción
        if (interactAction != null)
        {
            interactAction.Enable();
            interactAction.started += HandleInteractInput;
            Debug.Log("[PlayerInputHandler] ✅ Interact action habilitada");
        }
    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.Disable();
        }

        if (lookAction != null)
        {
            lookAction.Disable();
        }

        if (jumpAction != null)
        {
            jumpAction.started -= HandleJumpInput;
            jumpAction.Disable();
        }

        // ⭐ NUEVO: Deshabilitar action de ataque
        if (attackAction != null)
        {
            attackAction.started -= HandleAttackInput;
            attackAction.Disable();
        }

        // ⭐ NUEVO: Deshabilitar action de interacción
        if (interactAction != null)
        {
            interactAction.started -= HandleInteractInput;
            interactAction.Disable();
        }
    }

    private void HandleJumpInput(InputAction.CallbackContext context)
    {
        OnJump?.Invoke();
    }

    // ⭐ NUEVO: Handler para ataque
    private void HandleAttackInput(InputAction.CallbackContext context)
    {
        OnAttack?.Invoke();
    }

    // ⭐ NUEVO: Handler para interacción
    private void HandleInteractInput(InputAction.CallbackContext context)
    {
        OnInteract?.Invoke();
    }

    public bool IsMoving()
    {
        if (moveAction == null) return false;
        return moveAction.IsPressed();
    }

    public Vector2 GetMoveInput()
    {
        if (moveAction == null) return Vector2.zero;
        return moveAction.ReadValue<Vector2>();
    }

    public Vector2 GetLookInput()
    {
        if (lookAction == null) return Vector2.zero;
        return lookAction.ReadValue<Vector2>();
    }
}