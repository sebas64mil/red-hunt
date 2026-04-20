using System;
using UnityEngine;

public class EscapistHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;
    private int currentHealth;
    private int playerId = -1;
    private bool isLocal = false;
    private bool hasAlreadyDied = false;

    public event Action<int, int> OnHealthChanged;
    public event Action<int> OnPlayerDied;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsAlive => currentHealth > 0;

    private PlayerAnimationController animationController;
    private PlayerMovement playerMovement;
    private PlayerInputHandler playerInputHandler;
    private GameStateManager gameStateManager;

    private void Awake()
    {
        currentHealth = maxHealth;
        animationController = GetComponent<PlayerAnimationController>();
        playerMovement = GetComponent<PlayerMovement>();
        playerInputHandler = GetComponent<PlayerInputHandler>();
        gameStateManager = FindFirstObjectByType<GameStateManager>();
        
        if (animationController == null)
        {
            Debug.LogWarning("[EscapistHealth] PlayerAnimationController not found");
        }

        if (playerMovement == null)
        {
            Debug.LogWarning("[EscapistHealth] PlayerMovement not found");
        }

        if (playerInputHandler == null)
        {
            Debug.LogWarning("[EscapistHealth] PlayerInputHandler not found");
        }

        if (gameStateManager == null)
        {
            Debug.LogWarning("[EscapistHealth] GameStateManager not found");
        }
        
    }

    public void Init(int id, bool local)
    {
        playerId = id;
        isLocal = local;
        currentHealth = maxHealth;
        hasAlreadyDied = false;
    }

    public void TakeDamage(int damage)
    {
        if (!IsAlive)
        {
            Debug.LogWarning($"[EscapistHealth] Player {playerId} is already dead");
            return;
        }

        int previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - damage);


        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (!IsAlive)
        {
            HandlePlayerDeath();
        }
    }

    public void SetHealth(int newHealth)
    {
        int previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(newHealth, 0, maxHealth);
        
        Debug.Log($"[EscapistHealth] Health updated for player {playerId}: {previousHealth} → {currentHealth}/{maxHealth}");
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth == 0 && previousHealth > 0 && !hasAlreadyDied)
        {
            HandlePlayerDeath();
        }
    }

    private void HandlePlayerDeath()
    {
        if (hasAlreadyDied)
        {
            Debug.LogWarning($"[EscapistHealth] Player {playerId} already marked as dead");
            return;
        }

        hasAlreadyDied = true;

        
        DisablePlayerControls();
        
        if (animationController != null)
        {
            animationController.SetDeadAnimation(true);
        }

        if (gameStateManager != null)
        {
            gameStateManager.NotifyEscapistKilled(playerId);
        }
        else
        {
            Debug.LogError("[EscapistHealth] Could not notify GameStateManager - is NULL");
        }
        
        OnPlayerDied?.Invoke(playerId);
    }

    private void DisablePlayerControls()
    {
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (playerInputHandler != null)
        {
            playerInputHandler.enabled = false;
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        hasAlreadyDied = false;
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        if (playerInputHandler != null)
        {
            playerInputHandler.enabled = true;
        }
        
        if (animationController != null)
        {
            animationController.SetDeadAnimation(false);
        }
        
    }
}