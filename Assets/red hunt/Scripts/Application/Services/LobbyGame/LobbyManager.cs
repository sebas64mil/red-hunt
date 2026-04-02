using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LobbyManager
{
    private readonly PlayerRegistry playerRegistry;
    private readonly int maxPlayers;
    private readonly object syncRoot = new();

    public LobbyState CurrentState { get; private set; }

    public event Action<PlayerSession> OnPlayerJoined;
    public event Action<int> OnPlayerLeft;
    public event Action<int> OnPlayerReady;

    public LobbyManager(PlayerRegistry playerRegistry, int maxPlayers = 4)
    {
        this.playerRegistry = playerRegistry;
        this.maxPlayers = maxPlayers;
        CurrentState = LobbyState.Waiting;
    }

    public int MaxPlayers => maxPlayers;

    public bool IsFull()
    {
        var count = playerRegistry.GetAllPlayers().Count();
        return count >= maxPlayers;
    }

    public void ExecuteCommand(ILobbyCommand command)
    {
        command.Execute(this);
    }

    public PlayerSession AddPlayer(string playerType)
    {
        var player = playerRegistry.AddPlayer(playerType);

        OnPlayerJoined?.Invoke(player);
        UpdateState();

        return player;
    }

    public void RemovePlayer(int id)
    {
        playerRegistry.RemovePlayer(id);

        OnPlayerLeft?.Invoke(id);
        UpdateState();
    }

    public PlayerSession AddPlayerRemote(int id, string playerType)
    {
        lock (syncRoot)
        {
            // Validar que el jugador no exista ya
            var existing = playerRegistry.GetPlayer(id);
            if (existing != null)
            {
               // Debug.LogWarning($"[Lobby] Player {id} ya existe en el registro, ignorando AddPlayerRemote");
                return existing;
            }

            if (IsFull())
            {
                Debug.LogWarning("[Lobby] Lobby lleno, ignorando player remoto");
                return null;
            }

            try
            {
                var player = playerRegistry.AddPlayerWithId(id, playerType);

                OnPlayerJoined?.Invoke(player);
                UpdateState();

                return player;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] Error añadiendo player remoto: {e.Message}");
                return null;
            }
        }
    }

    public bool UpdatePlayerTypeRemote(int id, string newType)
    {
        lock (syncRoot)
        {
            var existing = playerRegistry.GetPlayer(id);
            if (existing == null) return false;

            bool changed = playerRegistry.UpdatePlayerType(id, newType);
            if (!changed) return false;

            var updated = playerRegistry.GetPlayer(id);
            OnPlayerLeft?.Invoke(id);
            OnPlayerJoined?.Invoke(updated);

            UpdateState();
            return true;
        }
    }

    public void SetPlayerReady(int id)
    {
        var player = playerRegistry.GetPlayer(id);

        if (player == null)
        {
            Debug.LogWarning($"[Lobby] Player {id} no encontrado");
            return;
        }

        OnPlayerReady?.Invoke(id);
    }

    public void RemovePlayerRemote(int id)
    {
        playerRegistry.RemovePlayer(id);

        OnPlayerLeft?.Invoke(id);
        UpdateState();
    }

    public IEnumerable<PlayerSession> GetAllPlayers()
    {
        return playerRegistry.GetAllPlayers();
    }

    private void UpdateState()
    {
        int count = 0;

        foreach (var _ in playerRegistry.GetAllPlayers())
            count++;

        if (count == 0)
            CurrentState = LobbyState.Waiting;
        else if (count >= maxPlayers)
            CurrentState = LobbyState.Full;
        else
            CurrentState = LobbyState.Waiting;
    }
}