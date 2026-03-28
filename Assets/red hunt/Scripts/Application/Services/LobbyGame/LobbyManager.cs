using System;
using System.Collections.Generic;
using System.Linq;

public class LobbyManager
{
    private readonly PlayerRegistry playerRegistry;
    private readonly int maxPlayers;

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
        if (IsFull())
        {
            UnityEngine.Debug.LogWarning("[Lobby] Lobby lleno, ignorando player remoto");
            return null;
        }

        var player = playerRegistry.AddPlayerWithId(id, playerType);

        OnPlayerJoined?.Invoke(player);
        UpdateState();

        return player;
    }

    public void SetPlayerReady(int id)
    {
        var player = playerRegistry.GetPlayer(id);

        if (player == null)
        {
            UnityEngine.Debug.LogWarning($"[Lobby] Player {id} no encontrado");
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


    // ------------------ STATE ------------------

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