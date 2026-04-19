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

    // Referencias
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
            Debug.LogWarning("[EscapistHealth] ⚠️ PlayerAnimationController no encontrado");
        }

        if (playerMovement == null)
        {
            Debug.LogWarning("[EscapistHealth] ⚠️ PlayerMovement no encontrado");
        }

        if (playerInputHandler == null)
        {
            Debug.LogWarning("[EscapistHealth] ⚠️ PlayerInputHandler no encontrado");
        }

        if (gameStateManager == null)
        {
            Debug.LogWarning("[EscapistHealth] ⚠️ GameStateManager no encontrado");
        }
        
        Debug.Log($"[EscapistHealth] Inicializado con salud: {currentHealth}/{maxHealth}");
    }

    public void Init(int id, bool local)
    {
        playerId = id;
        isLocal = local;
        currentHealth = maxHealth;
        hasAlreadyDied = false;
        Debug.Log($"[EscapistHealth] 🏥 Inicializado para player {playerId} (local: {isLocal})");
    }

    public void TakeDamage(int damage)
    {
        if (!IsAlive)
        {
            Debug.LogWarning($"[EscapistHealth] Player {playerId} ya está muerto");
            return;
        }

        int previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - damage);

        Debug.Log($"[EscapistHealth] 🔴 Player {playerId} recibió {damage} daño: {previousHealth} → {currentHealth}");

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
        
        Debug.Log($"[EscapistHealth] Salud actualizada para player {playerId}: {previousHealth} → {currentHealth}/{maxHealth}");
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // ⭐ CRÍTICO: Si la salud llega a 0 (desde la red), disparar evento de muerte
        if (currentHealth == 0 && previousHealth > 0 && !hasAlreadyDied)
        {
            HandlePlayerDeath();
        }
    }

    private void HandlePlayerDeath()
    {
        if (hasAlreadyDied)
        {
            Debug.LogWarning($"[EscapistHealth] ⚠️ Player {playerId} ya fue marcado como muerto");
            return;
        }

        hasAlreadyDied = true;

        Debug.Log($"[EscapistHealth] 💀 Player {playerId} ha muerto");
        
        // 1️⃣ Desactivar controles del jugador
        DisablePlayerControls();
        
        // 2️⃣ Actualizar animación de muerte
        if (animationController != null)
        {
            animationController.SetDeadAnimation(true);
        }

        // 3️⃣ ⭐ Notificar al GameStateManager que un escapista ha sido asesinado
        if (gameStateManager != null)
        {
            // Llamar al método público que dispara el evento
            gameStateManager.NotifyEscapistKilled();
            Debug.Log($"[EscapistHealth] ✅ GameStateManager notificado del asesinato del player {playerId}");
        }
        else
        {
            Debug.LogError("[EscapistHealth] ❌ No se pudo notificar a GameStateManager - es NULL");
        }
        
        // 4️⃣ Disparar evento local de muerte (para que HealthUIDisplay maneje la cámara)
        OnPlayerDied?.Invoke(playerId);
    }

    private void DisablePlayerControls()
    {
        // Desactivar movimiento
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
            Debug.Log($"[EscapistHealth] 🚫 PlayerMovement desactivado para player {playerId}");
        }

        // Desactivar input
        if (playerInputHandler != null)
        {
            playerInputHandler.enabled = false;
            Debug.Log($"[EscapistHealth] 🚫 PlayerInputHandler desactivado para player {playerId}");
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        hasAlreadyDied = false;
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        // Resetear controles
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        if (playerInputHandler != null)
        {
            playerInputHandler.enabled = true;
        }
        
        // Resetear animación de muerte
        if (animationController != null)
        {
            animationController.SetDeadAnimation(false);
        }
        
        Debug.Log($"[EscapistHealth] Salud reseteada para player {playerId}: {currentHealth}/{maxHealth}");
    }
}