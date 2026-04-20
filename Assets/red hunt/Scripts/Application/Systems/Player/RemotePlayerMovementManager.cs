using System;
using System.Collections.Generic;
using UnityEngine;

public class RemotePlayerMovementManager
{
    private Dictionary<int, RemotePlayerSync> remotePlayerSyncMap = new();
    
    public event Action<MovePacket> OnMoveReceived;

    public void RegisterRemotePlayer(int playerId, RemotePlayerSync remoteSync)
    {
        if (remoteSync == null)
        {
            Debug.LogWarning($"[RemotePlayerMovementManager] Attempting to register NULL RemotePlayerSync for player {playerId}");
            return;
        }

        if (remotePlayerSyncMap.ContainsKey(playerId))
        {
            Debug.LogWarning($"[RemotePlayerMovementManager] Player {playerId} was already registered. Replacing...");
        }

        remotePlayerSyncMap[playerId] = remoteSync;
    }

    public void UnregisterRemotePlayer(int playerId)
    {
        if (remotePlayerSyncMap.Remove(playerId))
        {
        }
    }


    public void ProcessMovePacket(MovePacket movePacket)
    {
        if (movePacket == null)
        {
            Debug.LogError("[RemotePlayerMovementManager] MovePacket is NULL");
            return;
        }

        int playerId = movePacket.playerId;

        if (!remotePlayerSyncMap.TryGetValue(playerId, out RemotePlayerSync remoteSync))
        {
            Debug.LogWarning($"[RemotePlayerMovementManager] RemotePlayerSync not found for player {playerId}");
            return;
        }

        if (remoteSync == null)
        {
            remotePlayerSyncMap.Remove(playerId);
            return;
        }

        if (!remoteSync.enabled)
        {
            Debug.LogWarning($"[RemotePlayerMovementManager] RemotePlayerSync disabled for player {playerId}");
            return;
        }

        remoteSync.OnRemotePositionReceived(movePacket);
        OnMoveReceived?.Invoke(movePacket);
    }

    public int GetRegisteredRemotePlayersCount() => remotePlayerSyncMap.Count;

    public void Clear()
    {
        remotePlayerSyncMap.Clear();
    }
}
