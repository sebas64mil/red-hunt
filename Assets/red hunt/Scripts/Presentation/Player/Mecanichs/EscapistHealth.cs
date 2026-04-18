using System;
using UnityEngine;

public class EscapistHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;
    private int currentHealth;
    private int playerId = -1;
    private bool isLocal = false;

    public event Action<int, int> OnHealthChanged;
    public event Action<int> OnPlayerDied;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsAlive => currentHealth > 0;

    // Referencia al controlador de animaciones
    private PlayerAnimationController animationController;

    private void Awake()
    {
        currentHealth = maxHealth;
        animationController = GetComponent<PlayerAnimationController>();
        
        if (animationController == null)
        {
            Debug.LogWarning("[EscapistHealth] ⚠️ PlayerAnimationController no encontrado");
        }
        
        Debug.Log($"[EscapistHealth] Inicializado con salud: {currentHealth}/{maxHealth}");
    }

    public void Init(int id, bool local)
    {
        playerId = id;
        isLocal = local;
        currentHealth = maxHealth;
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
            Debug.Log($"[EscapistHealth] 💀 Player {playerId} ha muerto");
            
            // Llamar al controlador de animaciones para actualizar la animación de muerte
            if (animationController != null)
            {
                animationController.SetDeadAnimation(true);
            }
            
            OnPlayerDied?.Invoke(playerId);
        }
    }

    public void SetHealth(int newHealth)
    {
        currentHealth = Mathf.Clamp(newHealth, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Debug.Log($"[EscapistHealth] Salud actualizada para player {playerId}: {currentHealth}/{maxHealth}");
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        // Resetear animación de muerte
        if (animationController != null)
        {
            animationController.SetDeadAnimation(false);
        }
        
        Debug.Log($"[EscapistHealth] Salud reseteada para player {playerId}: {currentHealth}/{maxHealth}");
    }
}