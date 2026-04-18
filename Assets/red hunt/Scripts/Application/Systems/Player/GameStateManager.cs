using System;
using System.Collections.Generic;
using UnityEngine;


public class GameStateManager : MonoBehaviour
{
    private Dictionary<int, PlayerHealthState> playerHealthStates = new();

    // Evento que se dispara cuando la salud de un jugador cambia
    public event Action<int, int, int> OnPlayerHealthChanged;

    public class PlayerHealthState
    {
        public int PlayerId { get; set; }
        public int CurrentHealth { get; set; }
        public int MaxHealth { get; set; }
        public bool IsAlive => CurrentHealth > 0;
    }

    public void InitializePlayer(int playerId, int maxHealth)
    {
        if (playerHealthStates.ContainsKey(playerId))
        {
            Debug.LogWarning($"[GameStateManager] Player {playerId} ya inicializado");
            return;
        }

        var state = new PlayerHealthState
        {
            PlayerId = playerId,
            CurrentHealth = maxHealth,
            MaxHealth = maxHealth
        };

        playerHealthStates[playerId] = state;
        Debug.Log($"[GameStateManager] ✅ Player {playerId} inicializado - Salud: {maxHealth}/{maxHealth}");
    }

    public void UpdatePlayerHealth(int playerId, int newHealth)
    {
        if (!playerHealthStates.TryGetValue(playerId, out var state))
        {
            Debug.LogWarning($"[GameStateManager] ⚠️ Player {playerId} no inicializado");
            return;
        }

        int oldHealth = state.CurrentHealth;
        state.CurrentHealth = Mathf.Clamp(newHealth, 0, state.MaxHealth);

        Debug.Log($"[GameStateManager] 🏥 Player {playerId}: {oldHealth} → {state.CurrentHealth}/{state.MaxHealth}");

        // Notificar cambio
        OnPlayerHealthChanged?.Invoke(playerId, state.CurrentHealth, state.MaxHealth);
    }

    public int GetPlayerHealth(int playerId)
    {
        if (playerHealthStates.TryGetValue(playerId, out var state))
        {
            return state.CurrentHealth;
        }
        return -1;
    }

    public int GetPlayerMaxHealth(int playerId)
    {
        if (playerHealthStates.TryGetValue(playerId, out var state))
        {
            return state.MaxHealth;
        }
        return -1;
    }

    public bool IsPlayerAlive(int playerId)
    {
        if (playerHealthStates.TryGetValue(playerId, out var state))
        {
            return state.IsAlive;
        }
        return false;
    }

    public Dictionary<int, PlayerHealthState> GetAllPlayerHealthStates()
    {
        return new Dictionary<int, PlayerHealthState>(playerHealthStates);
    }

    public void LogAllStates()
    {
        Debug.Log("[GameStateManager] ===== Estado de todos los jugadores =====");
        foreach (var kvp in playerHealthStates)
        {
            var state = kvp.Value;
        }
    }
}