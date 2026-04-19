using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    private Dictionary<int, PlayerHealthState> playerHealthStates = new();

    public event Action<int, int, int> OnPlayerHealthChanged;
    public event Action OnEscapistKilled;
    public event Action OnAllEscapistsDead;

    private bool allEscapistsDeadNotified = false;

    public class PlayerHealthState
    {
        public int PlayerId { get; set; }
        public int CurrentHealth { get; set; }
        public int MaxHealth { get; set; }
        public PlayerType PlayerType { get; set; }
        public bool IsAlive => CurrentHealth > 0;
    }

    public void InitializePlayer(int playerId, int maxHealth, PlayerType playerType = PlayerType.Escapist)
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
            MaxHealth = maxHealth,
            PlayerType = playerType
        };

        playerHealthStates[playerId] = state;

        Debug.Log($"[GameStateManager] ✅ Player {playerId} inicializado - Tipo: {playerType}, Salud: {maxHealth}/{maxHealth}");
    }

    public void UpdatePlayerHealth(int playerId, int newHealth)
    {
        if (!playerHealthStates.TryGetValue(playerId, out var state))
        {
            Debug.LogWarning($"[GameStateManager] ⚠️ Player {playerId} no inicializado");
            return;
        }

        int oldHealth = state.CurrentHealth;
        int clampedHealth = Mathf.Clamp(newHealth, 0, state.MaxHealth);

        if (oldHealth == clampedHealth)
        {
            return;
        }

        state.CurrentHealth = clampedHealth;

        Debug.Log($"[GameStateManager] 🏥 Player {playerId}: {oldHealth} → {state.CurrentHealth}/{state.MaxHealth}");

        OnPlayerHealthChanged?.Invoke(playerId, state.CurrentHealth, state.MaxHealth);

        if (state.PlayerType == PlayerType.Escapist && oldHealth > 0 && state.CurrentHealth <= 0)
        {
            Debug.Log($"[GameStateManager] 💀 Escapista {playerId} ha muerto");
            OnEscapistKilled?.Invoke();

            CheckAllEscapistsDead();
        }
    }

    // Mantener compatibilidad (si alguien lo llama)
    public void NotifyEscapistKilled()
    {
        Debug.Log("[GameStateManager] 💀 Un escapista ha sido asesinado");
        OnEscapistKilled?.Invoke();
        CheckAllEscapistsDead();
    }

    public void NotifyEscapistKilled(int playerId)
    {
        Debug.Log($"[GameStateManager] 💀 Un escapista ha sido asesinado (playerId={playerId})");

        if (playerId > 0 && playerHealthStates.TryGetValue(playerId, out var state))
        {
            if (state.PlayerType == PlayerType.Escapist)
            {
                UpdatePlayerHealth(playerId, 0);
                return; // UpdatePlayerHealth ya dispara OnEscapistKilled + check
            }
        }
        else
        {
            Debug.LogWarning($"[GameStateManager] ⚠️ NotifyEscapistKilled(playerId={playerId}) pero no está inicializado en el diccionario");
        }

        // Fallback: mantener comportamiento anterior
        OnEscapistKilled?.Invoke();
        CheckAllEscapistsDead();
    }

    private void CheckAllEscapistsDead()
    {
        if (allEscapistsDeadNotified)
        {
            return;
        }

        int totalEscapists = playerHealthStates.Values.Count(state => state.PlayerType == PlayerType.Escapist);
        int aliveEscapists = playerHealthStates.Values.Count(state => state.PlayerType == PlayerType.Escapist && state.IsAlive);

        Debug.Log($"[GameStateManager] 📊 Escapistas vivos: {aliveEscapists}/{totalEscapists}");

        if (totalEscapists <= 0)
        {
            return;
        }

        if (aliveEscapists == 0)
        {
            allEscapistsDeadNotified = true;
            Debug.Log("[GameStateManager] 🎉 ¡TODOS LOS ESCAPISTAS HAN MUERTO!");
            OnAllEscapistsDead?.Invoke();
        }
    }

    public int GetAliveEscapistCount()
    {
        return playerHealthStates.Values
            .Where(state => state.PlayerType == PlayerType.Escapist && state.IsAlive)
            .Count();
    }

    public int GetTotalEscapistCount()
    {
        return playerHealthStates.Values
            .Where(state => state.PlayerType == PlayerType.Escapist)
            .Count();
    }

    public void SubscribeToEscapistKilled(Action callback)
    {
        OnEscapistKilled += callback;
    }

    public void UnsubscribeFromEscapistKilled(Action callback)
    {
        OnEscapistKilled -= callback;
    }
}