using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class SpawnManager
{
    private Dictionary<int, GameObject> players = new();
    private Dictionary<int, PlayerType> playerTypes = new();


    private readonly Transform spawnParent;
    private readonly GameObject killerPrefab;
    private readonly GameObject escapistPrefab;


    private readonly Vector3 hostSpawnPosition;
    private readonly Vector3 clientBasePosition;
    private readonly float clientSpacing;

    private int localPlayerId;

    public SpawnManager(
        Transform spawnParent,
        GameObject killerPrefab,
        GameObject escapistPrefab,
        int localPlayerId,
        Vector3 hostSpawnPosition,
        Vector3 clientBasePosition,
        float clientSpacing)
    {
        this.spawnParent = spawnParent;
        this.killerPrefab = killerPrefab;
        this.escapistPrefab = escapistPrefab;
        this.localPlayerId = localPlayerId;


        this.hostSpawnPosition = hostSpawnPosition;
        this.clientBasePosition = clientBasePosition;
        this.clientSpacing = clientSpacing;
    }


    public void SetLocalPlayerId(int id)
    {
        localPlayerId = id;
    }

    public void AddPlayer(int id, PlayerType type)
    {
        if (players.ContainsKey(id)) return;

        int clientIndex = 0;
        if (type != PlayerType.Killer)
        {
            clientIndex = playerTypes.Values.Count(t => t != PlayerType.Killer);
        }

        GameObject prefab = type == PlayerType.Killer ? killerPrefab : escapistPrefab;
        Vector3 spawnPos = GetSpawnPosition(id, type, clientIndex);

        GameObject playerGO = GameObject.Instantiate(prefab, spawnPos, Quaternion.identity, spawnParent);
        playerGO.name = $"{type}_{id}";

        var view = playerGO.GetComponent<PlayerView>();
        if (view != null)
        {
            bool isLocal = id == localPlayerId;
            view.Init(id, isLocal);
        }
        else
        {
            Debug.LogWarning($"[SpawnManager] PlayerView no encontrado en {playerGO.name}");
        }

        // registrar player en estructuras
        players[id] = playerGO;
        playerTypes[id] = type;
    }

    public void RemovePlayer(int id)
    {
        if (!players.ContainsKey(id)) return;

        GameObject.Destroy(players[id]);
        players.Remove(id);

        if (playerTypes.ContainsKey(id))
            playerTypes.Remove(id);
    }


    private Vector3 GetSpawnPosition(int id, PlayerType type)
    {
        if (type == PlayerType.Killer)
            return new Vector3(0, 0, 0);

        return new Vector3(id * 2f, 0, 5f);
    }

    private Vector3 GetSpawnPosition(int id, PlayerType type, int clientIndex)
    {
        if (type == PlayerType.Killer)
            return hostSpawnPosition;

        return clientBasePosition + new Vector3(clientIndex * clientSpacing, 0f, 0f);
    }
    public void SpawnRemotePlayer(int id, PlayerType type)
    {
        if (players.ContainsKey(id)) return;

        AddPlayer(id, type);
    }
}