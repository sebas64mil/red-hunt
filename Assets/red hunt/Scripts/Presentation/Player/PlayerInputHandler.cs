using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    // ⭐ SOLO evento de salto, los otros se leen directamente
    public event Action OnJump;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;

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

            if (moveAction == null || lookAction == null || jumpAction == null)
            {
                Debug.LogError("[PlayerInputHandler] ❌ InputActions no encontradas. Verifica que existan 'Move', 'Look' y 'Jump'");
                return;
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
    }

    private void HandleJumpInput(InputAction.CallbackContext context)
    {
        OnJump?.Invoke();
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