using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public event Action OnAttack;

    public event Action OnJump;

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
            Debug.LogError("[PlayerInputHandler] PlayerInput not found in GameObject. Make sure it exists");
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
                Debug.LogError("[PlayerInputHandler] InputActions not found. Verify that 'Move', 'Look' and 'Jump' exist");
                return;
            }

            if (attackAction == null)
            {
                Debug.LogWarning("[PlayerInputHandler] InputAction 'Attack' not found. Killers will not be able to attack");
            }

            isInitialized = true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayerInputHandler] Error initializing InputActions: {ex.Message}");
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
        }

        if (lookAction != null)
        {
            lookAction.Enable();
        }

        if (jumpAction != null)
        {
            jumpAction.Enable();
            jumpAction.started += HandleJumpInput;
        }

        if (attackAction != null)
        {
            attackAction.Enable();
            attackAction.started += HandleAttackInput;
        }

        if (interactAction != null)
        {
            interactAction.Enable();
            interactAction.started += HandleInteractInput;
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

        if (attackAction != null)
        {
            attackAction.started -= HandleAttackInput;
            attackAction.Disable();
        }

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

    private void HandleAttackInput(InputAction.CallbackContext context)
    {
        OnAttack?.Invoke();
    }

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