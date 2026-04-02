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
            Debug.LogWarning($"[RemotePlayerMovementManager] Intentando registrar RemotePlayerSync NULL para player {playerId}");
            return;
        }

        if (remotePlayerSyncMap.ContainsKey(playerId))
        {
            Debug.LogWarning($"[RemotePlayerMovementManager] Player {playerId} ya estaba registrado. Reemplazando...");
        }

        remotePlayerSyncMap[playerId] = remoteSync;
    }

    public void UnregisterRemotePlayer(int playerId)
    {
        if (remotePlayerSyncMap.Remove(playerId))
        {
            Debug.Log($"[RemotePlayerMovementManager] ✅ Player remoto {playerId} desregistrado");
        }
    }


    public void ProcessMovePacket(MovePacket movePacket)
    {
        if (movePacket == null)
        {
            Debug.LogError("[RemotePlayerMovementManager] ❌ MovePacket es NULL");
            return;
        }

        int playerId = movePacket.playerId;

        if (!remotePlayerSyncMap.TryGetValue(playerId, out RemotePlayerSync remoteSync))
        {
            Debug.LogWarning($"[RemotePlayerMovementManager] ⚠️ RemotePlayerSync NO encontrado para player {playerId}");
            return;
        }

        if (remoteSync == null)
        {
            Debug.LogWarning($"[RemotePlayerMovementManager] ⚠️ RemotePlayerSync es NULL para player {playerId}");
            remotePlayerSyncMap.Remove(playerId);
            return;
        }

        if (!remoteSync.enabled)
        {
            Debug.LogWarning($"[RemotePlayerMovementManager] ⚠️ RemotePlayerSync desactivado para player {playerId}");
            return;
        }

        remoteSync.OnRemotePositionReceived(movePacket);
        OnMoveReceived?.Invoke(movePacket);
    }

    public int GetRegisteredRemotePlayersCount() => remotePlayerSyncMap.Count;

    public void Clear()
    {
        remotePlayerSyncMap.Clear();
        Debug.Log("[RemotePlayerMovementManager] Todos los jugadores remotos desregistrados");
    }
}
